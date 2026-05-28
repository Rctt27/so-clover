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

public static class GenerateAICluesPerDirection
{
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

            // Ordre stable : on itère sur une snapshot des directions restantes (HashSet est insertion-ordered,
            // mais on évite toute mutation concurrente du HashSet pendant l'itération).
            foreach (var dir in remaining.ToList())
            {
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    ConsumeBudget(game.Id);

                    AiBoardCluesDraft draft;
                    try
                    {
                        var single = new HashSet<Direction> { dir };
                        (draft, _) = await CallLlmAsync(
                            game, player, single, rejectedHistory, promptProvider, attempt, ct);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex,
                            "AI clue LLM call failed (direction={Direction}, attempt={Attempt}): game={GameId} player={PlayerId}",
                            dir, attempt, game.Id.Value, player.Id.Value);
                        continue;
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(ex,
                            "AI clue LLM returned unparseable JSON (direction={Direction}, attempt={Attempt}): game={GameId} player={PlayerId}",
                            dir, attempt, game.Id.Value, player.Id.Value);
                        continue;
                    }

                    // On retient le premier item dont la direction matche celle visée.
                    AiClueDraft? matched = null;
                    foreach (var item in draft.Clues)
                    {
                        if (Enum.TryParse<Direction>(item.Direction, ignoreCase: true, out var parsed) && parsed == dir)
                        {
                            matched = item;
                            break;
                        }
                    }
                    if (matched is null)
                        continue;

                    if (await TryApplyClueAsync(
                            game, player, dir, matched, validator, promptProvider, rejectedHistory, ct))
                    {
                        remaining.Remove(dir);
                        break;
                    }
                }
            }
        }
    }
}
