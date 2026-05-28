using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.AI.Reasoning;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Gameplay;

namespace SoClover.UseCases.AI;

public abstract class AiCluesGeneratorBase : IGenerateAICluesUseCase
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected readonly IGameRepository _repo;
    protected readonly IClueValidatorFactory _validatorFactory;
    protected readonly IChatClient _chatClient;
    protected readonly IAiCluePromptProviderFactory _promptProviderFactory;
    protected readonly IAiClueExplanationStore _explanationStore;
    protected readonly IEventPublisher _events;
    protected readonly IOptions<LlmOptions> _llmOptions;
    protected readonly GameLlmBudget _budget;
    protected readonly ISubmitBoardUseCase _submitBoard;
    protected readonly IReasoningRequestConfigurator _reasoningConfigurator;
    protected readonly ILogger _logger;

    // État par requête : valable uniquement parce que le use case est enregistré en DI transient (1 instance par appel Handle). _llmCalls est remis à 0 en tête de Handle ; ne pas passer ce type en Scoped/Singleton.
    private int _llmCalls;
    private int? _lastPromptVersion;
    private (string Path, DateTime LastWriteTimeUtc, string Content)? _reasoningPreambleCache;

    // Les sous-classes concrètes doivent exposer un ILogger<LeurType>? dans leur ctor et le passer ici,
    // sinon la catégorie de log est perdue (fallback NullLogger).
    protected AiCluesGeneratorBase(
        IGameRepository repo,
        IClueValidatorFactory validatorFactory,
        IChatClient chatClient,
        IAiCluePromptProviderFactory promptProviderFactory,
        IAiClueExplanationStore explanationStore,
        IEventPublisher events,
        IOptions<LlmOptions> llmOptions,
        GameLlmBudget budget,
        ISubmitBoardUseCase submitBoard,
        IReasoningRequestConfigurator? reasoningConfigurator = null,
        ILogger? logger = null)
    {
        _repo = repo;
        _validatorFactory = validatorFactory;
        _chatClient = chatClient;
        _promptProviderFactory = promptProviderFactory;
        _explanationStore = explanationStore;
        _events = events;
        _llmOptions = llmOptions;
        _budget = budget;
        _submitBoard = submitBoard;
        _reasoningConfigurator = reasoningConfigurator ?? new NullReasoningConfigurator();
        _logger = logger ?? NullLogger.Instance;
    }

    // Nombre maximal de tentatives d'appel LLM par direction (1 essai + MaxRetries).
    protected int MaxAttempts => _llmOptions.Value.MaxRetries + 1;

    public async Task<GenerateAIClues.Response> Handle(
        GenerateAIClues.Request request, CancellationToken ct = default)
    {
        _llmCalls = 0;

        var game = await _repo.Get(request.GameId, ct)
            ?? throw new GameNotFoundException(request.GameId);
        var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId)
            ?? throw new PlayerNotFoundException(request.PlayerId);
        if (!player.IsAI)
            throw new InvalidOperationException(
                $"Player {request.PlayerId} is not an AI player.");

        await _events.Publish(new AiClueGenerationRequested(game.Id, player.Id), ct);

        var remaining = ComputeRemainingDirections(player.Board);
        if (remaining.Count == 0)
        {
            return new GenerateAIClues.Response(SucceededCount: 4, FailedCount: 0, LlmCallsConsumed: 0);
        }

        var promptProvider = _promptProviderFactory.GetFor(game.Language)
            ?? throw new UnsupportedAiLanguageException(game.Language);
        var validator = _validatorFactory.GetFor(game.Language, game.SemanticClueCheckEnabled);

        var rejectedHistory = new Dictionary<Direction, List<RejectedAttempt>>();
        var maxAttempts = MaxAttempts;

        try
        {
            await FillRemainingAsync(
                game, player, remaining, rejectedHistory, promptProvider, validator, ct);
        }
        catch (LlmBudgetExhaustedException)
        {
            _logger.LogWarning(
                "AI clue generation stopped: LLM budget exhausted for game={GameId} player={PlayerId}",
                game.Id.Value, player.Id.Value);

            foreach (var dir in remaining)
            {
                await _events.Publish(
                    new AiClueGenerationFailed(
                        game.Id, player.Id, dir,
                        Reason: "LLM budget exhausted.",
                        AttemptedClues: Array.Empty<string>()),
                    ct);
            }
            await _events.Publish(
                new AiPlayerBoardFailed(game.Id, player.Id, "LLM budget exhausted."), ct);

            var budgetFailed = remaining.Count;
            return new GenerateAIClues.Response(
                SucceededCount: 4 - budgetFailed,
                FailedCount: budgetFailed,
                LlmCallsConsumed: _llmCalls);
        }

        foreach (var dir in remaining)
        {
            var attempted = rejectedHistory.TryGetValue(dir, out var list)
                ? (IReadOnlyList<string>)list.Select(r => r.ClueText).ToList().AsReadOnly()
                : Array.Empty<string>();
            await _events.Publish(
                new AiClueGenerationFailed(
                    game.Id, player.Id, dir,
                    Reason: "Max retries exhausted with no valid clue.",
                    AttemptedClues: attempted),
                ct);
        }

        if (remaining.Count > 0)
        {
            await _events.Publish(
                new AiPlayerBoardFailed(
                    game.Id, player.Id,
                    $"{remaining.Count} direction(s) could not be generated after {maxAttempts} attempt(s)."),
                ct);
        }

        if (remaining.Count == 0)
        {
            try
            {
                await _submitBoard.Handle(
                    new SubmitBoard.Request(game.Id, player.Id, InvocationOrigin.System), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AI auto-submit failed: game={GameId} player={PlayerId}",
                    game.Id.Value, player.Id.Value);
            }
        }

        var failed = remaining.Count;
        return new GenerateAIClues.Response(
            SucceededCount: 4 - failed,
            FailedCount: failed,
            LlmCallsConsumed: _llmCalls);
    }

    // TryConsume lève LlmBudgetExhaustedException AVANT l'incrément si le budget est épuisé.
    protected void ConsumeBudget(GameId gameId)
    {
        _budget.TryConsume(gameId);
        _llmCalls++;
    }

    protected async Task<bool> TryApplyClueAsync(
        Game game,
        Player player,
        Direction dir,
        AiClueDraft item,
        IClueValidator validator,
        IAiCluePromptProvider promptProvider,
        Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
        CancellationToken ct)
    {
        var result = game.SetClue(player.Id, dir, item.ClueWord, validator);
        if (result.IsValid)
        {
            _explanationStore.Save(game.Id, player.Id, dir, item.Explanation);
            await _repo.Save(game, ct);
            await _events.Publish(
                new AiClueGenerated(game.Id, player.Id, dir, item.ClueWord, item.Explanation),
                ct);

            _logger.LogInformation(
                "AI clue validated: game={GameId} player={PlayerId} direction={Direction} clueText={ClueText} isValid={IsValid} promptVersion={PromptVersion} provider={LlmProvider} model={LlmModel}",
                game.Id.Value, player.Id.Value, dir, item.ClueWord, true,
                _lastPromptVersion, _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel);
            return true;
        }

        AppendRejection(rejectedHistory, dir, item.ClueWord, result, promptProvider);

        var rules = string.Join(",", result.Errors.Select(e => e.Rule.ToString()));
        _logger.LogInformation(
            "AI clue rejected: game={GameId} player={PlayerId} direction={Direction} clueText={ClueText} isValid={IsValid} rejectionRules={RejectionRules} promptVersion={PromptVersion} provider={LlmProvider} model={LlmModel}",
            game.Id.Value, player.Id.Value, dir, item.ClueWord, false,
            rules, _lastPromptVersion, _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel);
        return false;
    }

    /// <summary>
    /// Génère et applique les indices IA pour toutes les directions encore présentes dans <paramref name="remaining"/>.
    /// </summary>
    /// <remarks>
    /// Contrat de mutation obligatoire : chaque implémentation doit retirer de <paramref name="remaining"/>
    /// toute direction dont l'indice a été appliqué avec succès. La classe de base lit <paramref name="remaining"/>
    /// après l'appel pour calculer <c>SucceededCount</c>/<c>FailedCount</c>, décider de l'auto-soumission
    /// et émettre les événements finaux — omettre ce retrait produit des compteurs erronés et un événement
    /// <c>AiPlayerBoardFailed</c> spurieux.
    /// </remarks>
    protected abstract Task FillRemainingAsync(
        Game game,
        Player player,
        HashSet<Direction> remaining,
        Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
        IAiCluePromptProvider promptProvider,
        IClueValidator validator,
        CancellationToken ct);

    protected async Task<(AiBoardCluesDraft Draft, int? PromptVersion)> CallLlmAsync(
        Game game,
        Player player,
        HashSet<Direction> remaining,
        Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
        IAiCluePromptProvider promptProvider,
        int attempt,
        CancellationToken ct,
        Func<IAiCluePromptProvider, BoardCluesPromptContext, AiCluePromptBundle>? buildBundle = null,
        Func<string, AiBoardCluesDraft>? parseResponse = null)
    {
        buildBundle ??= static (p, ctx) => p.BuildBoardCluesPrompt(ctx);
        parseResponse ??= static text => JsonSerializer.Deserialize<AiBoardCluesDraft>(text, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned invalid JSON.");

        var cards = BuildBoardCardSnapshots(player.Board);
        var rejectedRO = rejectedHistory.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<RejectedAttempt>)kv.Value.AsReadOnly());
        var reasoningEnabled = _llmOptions.Value.ReasoningEnabled;
        var context = new BoardCluesPromptContext(
            game.Language, cards, remaining.ToList().AsReadOnly(), rejectedRO,
            IncludeReasoning: reasoningEnabled);
        var bundle = buildBundle(promptProvider, context);

        var systemPrompt = bundle.SystemPrompt;
        if (reasoningEnabled)
        {
            var preamble = ReadReasoningPreamble(_llmOptions.Value.ReasoningSystemPromptPath);
            if (!string.IsNullOrWhiteSpace(preamble))
                systemPrompt = $"{preamble.Trim()}\n\n{systemPrompt}";
        }

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, bundle.UserPrompt),
        };

        var opts = _llmOptions.Value;
        var effectiveModel = player.AIConfig?.Model ?? opts.DefaultModel;

        var chatOptions = new ChatOptions
        {
            ModelId = effectiveModel,
            Temperature = (float)(player.AIConfig?.Temperature ?? opts.DefaultTemperature),
        };
        if (opts.TopP is { } topP)
            chatOptions.TopP = (float)topP;
        if (opts.MaxOutputTokens is { } maxOutputTokens)
            chatOptions.MaxOutputTokens = maxOutputTokens;

        if (reasoningEnabled)
            _reasoningConfigurator.Configure(chatOptions);

        var sw = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, options: chatOptions, ct)
            .ConfigureAwait(false);
        sw.Stop();

        _logger.LogInformation(
            "AI clue LLM call completed: game={GameId} player={PlayerId} attempt={Attempt} latencyMs={LatencyMs} provider={LlmProvider} model={LlmModel} promptVersion={PromptVersion} remainingDirections={RemainingDirections}",
            game.Id.Value, player.Id.Value, attempt, sw.ElapsedMilliseconds,
            _llmOptions.Value.Provider, effectiveModel, bundle.PromptVersion,
            string.Join(",", remaining));

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("LLM returned an empty response.");
        text = StripThinkTags(text);
        text = StripJsonFences(text);
        var draft = parseResponse(text);
        _lastPromptVersion = bundle.PromptVersion;
        return (draft, bundle.PromptVersion);
    }

    private static string StripJsonFences(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```")) return t;

        var firstNewline = t.IndexOf('\n');
        if (firstNewline >= 0) t = t[(firstNewline + 1)..];
        if (t.EndsWith("```")) t = t[..^3];
        return t.Trim();
    }

    private static string StripThinkTags(string text)
    {
        foreach (var closeTag in new[] { "</think>", "[/THINK]" })
        {
            var closeIdx = text.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
            if (closeIdx >= 0)
                text = text[(closeIdx + closeTag.Length)..];
        }
        return text.Trim();
    }

    private string ReadReasoningPreamble(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                _logger.LogWarning(
                    "Reasoning system prompt file not found: {Path}. Continuing without preamble.", path);
                return string.Empty;
            }

            var lastWrite = info.LastWriteTimeUtc;
            if (_reasoningPreambleCache is { } cached
                && cached.Path == path && cached.LastWriteTimeUtc == lastWrite)
                return cached.Content;

            var content = File.ReadAllText(path);
            _reasoningPreambleCache = (path, lastWrite, content);
            return content;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "Failed to read reasoning system prompt file: {Path}. Continuing without preamble.", path);
            return string.Empty;
        }
    }

    private static HashSet<Direction> ComputeRemainingDirections(CloverBoard board)
    {
        var remaining = new HashSet<Direction>();
        if (board.TopClue is null)    remaining.Add(Direction.Top);
        if (board.RightClue is null)  remaining.Add(Direction.Right);
        if (board.BottomClue is null) remaining.Add(Direction.Bottom);
        if (board.LeftClue is null)   remaining.Add(Direction.Left);
        return remaining;
    }

    private static IReadOnlyList<BoardCardSnapshot> BuildBoardCardSnapshots(CloverBoard board)
    {
        return new List<BoardCardSnapshot>
        {
            Snapshot(BoardPosition.TopLeft,     board.TopLeft!),
            Snapshot(BoardPosition.TopRight,    board.TopRight!),
            Snapshot(BoardPosition.BottomRight, board.BottomRight!),
            Snapshot(BoardPosition.BottomLeft,  board.BottomLeft!),
        }.AsReadOnly();
    }

    private static BoardCardSnapshot Snapshot(BoardPosition pos, OrientedCard oc) =>
        new(pos,
            TopWord:    oc.GetWord(Direction.Top),
            RightWord:  oc.GetWord(Direction.Right),
            BottomWord: oc.GetWord(Direction.Bottom),
            LeftWord:   oc.GetWord(Direction.Left));

    private static void AppendRejection(
        Dictionary<Direction, List<RejectedAttempt>> history,
        Direction dir,
        string clueText,
        ClueValidationResult result,
        IAiCluePromptProvider promptProvider)
    {
        if (!history.TryGetValue(dir, out var list))
            history[dir] = list = new List<RejectedAttempt>();
        list.Add(new RejectedAttempt(clueText, promptProvider.FormatRejectionReason(result)));
    }
}
