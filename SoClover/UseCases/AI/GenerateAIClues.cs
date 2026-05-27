using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.AI.Reasoning;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;

namespace SoClover.UseCases.AI;

public interface IGenerateAICluesUseCase : IUseCase<GenerateAIClues.Request, GenerateAIClues.Response> { }

public static class GenerateAIClues
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);

    public readonly record struct Response(int SucceededCount, int FailedCount, int LlmCallsConsumed);

    public sealed class Handler : AiCluesGeneratorBase
    {
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
            IReasoningRequestConfigurator? reasoningConfigurator = null,
            ILogger<Handler>? logger = null)
            : base(repo, validatorFactory, chatClient, promptProviderFactory, explanationStore,
                   events, llmOptions, budget, submitBoard, reasoningConfigurator, logger)
        {
        }

        // Pipeline PerBoard : un seul appel LLM couvre toutes les directions restantes, jusqu'à
        // MaxRetries+1 tentatives. Chaque clue valide est appliquée et retirée de `remaining`.
        protected override async Task FillRemainingAsync(
            Game game,
            Player player,
            HashSet<Direction> remaining,
            Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
            IAiCluePromptProvider promptProvider,
            IClueValidator validator,
            CancellationToken ct)
        {
            var maxAttempts = MaxAttempts;

            for (var attempt = 0; attempt < maxAttempts && remaining.Count > 0; attempt++)
            {
                ConsumeBudget(game.Id);

                AiBoardCluesDraft draft;
                try
                {
                    (draft, _) = await CallLlmAsync(
                        game, player, remaining, rejectedHistory, promptProvider, attempt, ct);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex,
                        "AI clue LLM call failed (attempt {Attempt}): game={GameId} player={PlayerId}",
                        attempt, game.Id.Value, player.Id.Value);
                    continue;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "AI clue LLM returned unparseable JSON (attempt {Attempt}): game={GameId} player={PlayerId}",
                        attempt, game.Id.Value, player.Id.Value);
                    continue;
                }

                foreach (var item in draft.Clues)
                {
                    if (!Enum.TryParse<Direction>(item.Direction, ignoreCase: true, out var dir))
                        continue;
                    if (!remaining.Contains(dir))
                        continue;

                    if (await TryApplyClueAsync(
                            game, player, dir, item, validator, promptProvider, rejectedHistory, ct))
                    {
                        remaining.Remove(dir);
                    }
                }
            }
        }
    }
}
