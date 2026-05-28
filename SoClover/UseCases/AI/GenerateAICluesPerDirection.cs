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

        protected override Task FillRemainingAsync(
            Game game,
            Player player,
            HashSet<Direction> remaining,
            Dictionary<Direction, List<RejectedAttempt>> rejectedHistory,
            IAiCluePromptProvider promptProvider,
            IClueValidator validator,
            CancellationToken ct)
        {
            // Implémenté dans la tâche 2.
            throw new NotImplementedException();
        }
    }
}
