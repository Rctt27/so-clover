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
    protected readonly IGameRepository _repo;
    protected readonly IClueValidatorFactory _validatorFactory;
    protected readonly IAiCluePromptProviderFactory _promptProviderFactory;
    protected readonly IAiClueExplanationStore _explanationStore;
    protected readonly IEventPublisher _events;
    protected readonly IOptions<LlmOptions> _llmOptions;
    protected readonly GameLlmBudget _budget;
    protected readonly ISubmitBoardUseCase _submitBoard;
    protected readonly ILogger _logger;

    // État par requête : valable uniquement parce que le use case est enregistré en DI transient (1 instance par appel Handle). _llmCalls est remis à 0 en tête de Handle ; ne pas passer ce type en Scoped/Singleton.
    private int _llmCalls;
    private int? _lastPromptVersion;
    private readonly AiClueLlmCaller _caller;

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
        _promptProviderFactory = promptProviderFactory;
        _explanationStore = explanationStore;
        _events = events;
        _llmOptions = llmOptions;
        _budget = budget;
        _submitBoard = submitBoard;
        _logger = logger ?? NullLogger.Instance;

        // Composé ici plutôt qu'injecté : aucune signature de ctor de sous-classe ne change,
        // donc aucun câblage DI ni aucun test existant à toucher.
        _caller = new AiClueLlmCaller(chatClient, llmOptions, reasoningConfigurator ?? new NullReasoningConfigurator());
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

            await PublishProgressAsync(game, player, rejectedHistory, ct);
            return true;
        }

        AppendRejection(rejectedHistory, dir, item.ClueWord, result, promptProvider);

        var rules = string.Join(",", result.Errors.Select(e => e.Rule.ToString()));
        _logger.LogInformation(
            "AI clue rejected: game={GameId} player={PlayerId} direction={Direction} clueText={ClueText} isValid={IsValid} rejectionRules={RejectionRules} promptVersion={PromptVersion} provider={LlmProvider} model={LlmModel}",
            game.Id.Value, player.Id.Value, dir, item.ClueWord, false,
            rules, _lastPromptVersion, _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel);

        await PublishProgressAsync(game, player, rejectedHistory, ct);
        return false;
    }

    /// <summary>
    /// Émet la progression absolue du board (indices validés + retries par direction).
    /// Appelée après chaque tentative (succès ou rejet) depuis la méthode partagée
    /// <see cref="TryApplyClueAsync"/> → les deux pipelines (PerBoard / PerDirection) en bénéficient.
    /// </summary>
    private async Task PublishProgressAsync(
        Game game,
        Player player,
        Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
        CancellationToken ct)
    {
        var board = player.Board;
        var submitted = 0;
        if (board.TopClue is not null)    submitted++;
        if (board.RightClue is not null)  submitted++;
        if (board.BottomClue is not null) submitted++;
        if (board.LeftClue is not null)   submitted++;

        var retries = new Dictionary<Direction, int>
        {
            [Direction.Top]    = rejectedHistory.GetValueOrDefault(Direction.Top)?.Count ?? 0,
            [Direction.Right]  = rejectedHistory.GetValueOrDefault(Direction.Right)?.Count ?? 0,
            [Direction.Bottom] = rejectedHistory.GetValueOrDefault(Direction.Bottom)?.Count ?? 0,
            [Direction.Left]   = rejectedHistory.GetValueOrDefault(Direction.Left)?.Count ?? 0,
        };

        await _events.Publish(
            new AiClueProgressUpdate(game.Id, player.Id, submitted, retries), ct);
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
        parseResponse ??= AiClueResponseParser.ParseBoard;

        var request = new ClueCallRequest(
            game.Language,
            BuildBoardCardSnapshots(player.Board),
            remaining.ToList().AsReadOnly(),
            rejectedHistory.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<RejectedAttempt>)kv.Value.AsReadOnly()),
            player.AIConfig?.Model,
            player.AIConfig?.Temperature);

        ClueCallResult result;
        try
        {
            result = await _caller.CallAsync(request, promptProvider, buildBundle, parseResponse, ct)
                .ConfigureAwait(false);
        }
        catch (LlmCallException ex)
        {
            // Le log « call completed » précède historiquement la vérification du texte vide :
            // on le reproduit à l'identique sur le chemin d'échec, dans le même ordre.
            LogCallCompleted(game, player, attempt, ex.LatencyMs, ex.EffectiveModel, ex.PromptVersion, remaining);

            if (ex is EmptyLlmResponseException empty)
            {
                // Cas typique d'un modèle reasoning (ex. gemma) dont la réflexion native sature la fenêtre de
                // contexte / le budget de sortie : la complétion est tronquée (finish_reason=length) AVANT
                // l'émission du JSON. response.Text est alors vide. On loggue finish_reason + usage pour rendre
                // ce diagnostic immédiat, au lieu du générique « empty response » qui se répète en silence
                // jusqu'à épuisement des retries (board bloqué à 0/4).
                _logger.LogWarning(
                    "AI clue LLM returned empty content (native reasoning likely overflowed the context/output budget before emitting the answer): game={GameId} player={PlayerId} attempt={Attempt} finishReason={FinishReason} inputTokens={InputTokens} outputTokens={OutputTokens} model={LlmModel}. Increase the model's context window / maxOutputTokens, or enable ReasoningEnabled to load the concise reasoning prompt.",
                    game.Id.Value, player.Id.Value, attempt,
                    empty.FinishReason, empty.InputTokens, empty.OutputTokens,
                    empty.EffectiveModel);
            }

            LogPreambleWarning(ex.PreambleWarning);
            throw;
        }

        LogCallCompleted(game, player, attempt, result.LatencyMs, result.EffectiveModel, result.PromptVersion, remaining);
        LogPreambleWarning(result.PreambleWarning);

        _lastPromptVersion = result.PromptVersion;
        return (result.Draft, result.PromptVersion);
    }

    /// <summary>
    /// Journalise le préambule reasoning manquant/illisible, quelle que soit l'issue de l'appel LLM :
    /// le cas le plus probable où ce diagnostic sert (mode reasoning mal configuré → préambule absent
    /// → modèle renvoyant du vide) est précisément un chemin d'échec. Distingue « introuvable »
    /// (message d'avant l'extraction) d'« illisible » (message + exception d'avant l'extraction).
    /// </summary>
    private void LogPreambleWarning(ReasoningPreambleWarning? warning)
    {
        if (warning is null)
            return;

        if (warning.Cause is { } cause)
        {
            _logger.LogWarning(cause,
                "Failed to read reasoning system prompt file: {Path}. Continuing without preamble.", warning.Path);
        }
        else
        {
            _logger.LogWarning(
                "Reasoning system prompt file not found: {Path}. Continuing without preamble.", warning.Path);
        }
    }

    private void LogCallCompleted(
        Game game, Player player, int attempt,
        long latencyMs, string effectiveModel, int? promptVersion, HashSet<Direction> remaining)
    {
        _logger.LogInformation(
            "AI clue LLM call completed: game={GameId} player={PlayerId} attempt={Attempt} latencyMs={LatencyMs} provider={LlmProvider} model={LlmModel} promptVersion={PromptVersion} remainingDirections={RemainingDirections}",
            game.Id.Value, player.Id.Value, attempt, latencyMs,
            _llmOptions.Value.Provider, effectiveModel, promptVersion,
            string.Join(",", remaining));
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
