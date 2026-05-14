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
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Gameplay;

namespace SoClover.UseCases.AI;

public interface IGenerateAICluesUseCase : IUseCase<GenerateAIClues.Request, GenerateAIClues.Response> { }

public static class GenerateAIClues
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);

    public readonly record struct Response(int SucceededCount, int FailedCount, int LlmCallsConsumed);

    public sealed class Handler : IGenerateAICluesUseCase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly IGameRepository _repo;
        private readonly IClueValidatorFactory _validatorFactory;
        private readonly IChatClient _chatClient;
        private readonly IAiCluePromptProviderFactory _promptProviderFactory;
        private readonly IAiClueExplanationStore _explanationStore;
        private readonly IEventPublisher _events;
        private readonly IOptions<LlmOptions> _llmOptions;
        private readonly GameLlmBudget _budget;
        private readonly ISubmitBoardUseCase _submitBoard;
        private readonly ILogger<Handler> _logger;

        public Handler(
            IGameRepository repo,
            IClueValidatorFactory validatorFactory,
            IChatClient chatClient,
            IAiCluePromptProviderFactory promptProviderFactory,
            IAiClueExplanationStore explanationStore,
            IEventPublisher events,
            IOptions<LlmOptions> llmOptions,
            GameLlmBudget budget,
            ISubmitBoardUseCase submitBoard,
            ILogger<Handler>? logger = null)
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
            _logger = logger ?? NullLogger<Handler>.Instance;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
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
                return new Response(SucceededCount: 4, FailedCount: 0, LlmCallsConsumed: 0);
            }

            var promptProvider = _promptProviderFactory.GetFor(game.Language)
                ?? throw new UnsupportedAiLanguageException(game.Language);
            var validator = _validatorFactory.GetFor(game.Language, game.SemanticClueCheckEnabled);

            var rejectedHistory = new Dictionary<Direction, List<RejectedAttempt>>();
            var maxAttempts = _llmOptions.Value.MaxRetries + 1;
            var llmCalls = 0;

            try
            {
                for (var attempt = 0; attempt < maxAttempts && remaining.Count > 0; attempt++)
                {
                    _budget.TryConsume(game.Id);
                    llmCalls++;

                    var (draft, promptVersion) = await CallLlmAsync(
                        game, player, remaining, rejectedHistory, promptProvider, attempt, ct);

                    foreach (var item in draft.Clues)
                    {
                        if (!Enum.TryParse<Direction>(item.Direction, ignoreCase: true, out var dir))
                            continue;
                        if (!remaining.Contains(dir))
                            continue;

                        var result = game.SetClue(player.Id, dir, item.ClueWord, validator);
                        if (result.IsValid)
                        {
                            _explanationStore.Save(game.Id, player.Id, dir, item.Explanation);
                            await _repo.Save(game, ct);
                            await _events.Publish(
                                new AiClueGenerated(game.Id, player.Id, dir, item.ClueWord, item.Explanation),
                                ct);
                            remaining.Remove(dir);

                            _logger.LogInformation(
                                "AI clue validated: game={GameId} player={PlayerId} direction={Direction} clueText={ClueText} isValid={IsValid} promptVersion={PromptVersion} provider={LlmProvider} model={LlmModel}",
                                game.Id.Value, player.Id.Value, dir, item.ClueWord, true,
                                promptVersion, _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel);
                        }
                        else
                        {
                            AppendRejection(rejectedHistory, dir, item.ClueWord, result);

                            var rules = string.Join(",", result.Errors.Select(e => e.Rule.ToString()));
                            _logger.LogInformation(
                                "AI clue rejected: game={GameId} player={PlayerId} direction={Direction} clueText={ClueText} isValid={IsValid} rejectionRules={RejectionRules} promptVersion={PromptVersion} provider={LlmProvider} model={LlmModel}",
                                game.Id.Value, player.Id.Value, dir, item.ClueWord, false,
                                rules, promptVersion, _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel);
                        }
                    }
                }
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
                return new Response(
                    SucceededCount: 4 - budgetFailed,
                    FailedCount: budgetFailed,
                    LlmCallsConsumed: llmCalls);
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
            return new Response(
                SucceededCount: 4 - failed,
                FailedCount: failed,
                LlmCallsConsumed: llmCalls);
        }

        private async Task<(AiBoardCluesDraft Draft, int? PromptVersion)> CallLlmAsync(
            Game game,
            Player player,
            HashSet<Direction> remaining,
            Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
            IAiCluePromptProvider promptProvider,
            int attempt,
            CancellationToken ct)
        {
            var cards = BuildBoardCardSnapshots(player.Board);
            var rejectedRO = rejectedHistory.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<RejectedAttempt>)kv.Value.AsReadOnly());
            var context = new BoardCluesPromptContext(
                game.Language, cards, remaining.ToList().AsReadOnly(), rejectedRO);
            var bundle = promptProvider.BuildBoardCluesPrompt(context);

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, bundle.SystemPrompt),
                new ChatMessage(ChatRole.User, bundle.UserPrompt),
            };

            var sw = Stopwatch.StartNew();
            var response = await _chatClient.GetResponseAsync(messages, options: null, ct)
                .ConfigureAwait(false);
            sw.Stop();

            _logger.LogInformation(
                "AI clue LLM call completed: game={GameId} player={PlayerId} attempt={Attempt} latencyMs={LatencyMs} provider={LlmProvider} model={LlmModel} promptVersion={PromptVersion} remainingDirections={RemainingDirections}",
                game.Id.Value, player.Id.Value, attempt, sw.ElapsedMilliseconds,
                _llmOptions.Value.Provider, _llmOptions.Value.DefaultModel, bundle.PromptVersion,
                string.Join(",", remaining));

            var text = response.Text
                ?? throw new InvalidOperationException("LLM returned an empty response.");
            var draft = JsonSerializer.Deserialize<AiBoardCluesDraft>(text, JsonOptions)
                ?? throw new InvalidOperationException("LLM returned invalid JSON.");
            return (draft, bundle.PromptVersion);
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
            ClueValidationResult result)
        {
            if (!history.TryGetValue(dir, out var list))
                history[dir] = list = new List<RejectedAttempt>();
            list.Add(new RejectedAttempt(clueText, FormatRejection(result)));
        }

        private static string FormatRejection(ClueValidationResult r) =>
            string.Join("; ", r.Errors.Select(e =>
                e.ConflictingDirection is { } d
                    ? $"{e.Rule} avec le mot \"{e.CardWord}\" (direction {d})"
                    : $"{e.Rule} avec le mot \"{e.CardWord}\""));
    }
}
