using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.AI;
using SoClover.UseCases.GameLogics;
using SoClover.UseCases.Gameplay;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAICluesPerDirectionTests
{
    private static ServiceProvider BuildPerDirection(
        FakeChatClient chat, int budgetMaxCallsPerGame = 50)
    {
        return BuildPerDirectionDirect(chat, budgetMaxCallsPerGame);
    }

    private static ServiceProvider BuildPerDirectionDirect(
        FakeChatClient chat, int budgetMaxCallsPerGame)
    {
        // Recopie la registration d'AiTestProvider.Build mais enregistre GenerateAICluesPerDirection.Handler.
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<InMemoryEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventPublisher>());
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(_ =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton<IClueValidatorFactory, ClueValidatorFactory>();
        services.AddSingleton<IChatClient>(chat);
        services.AddSingleton(Options.Create(new LlmOptions
        {
            MaxRetries = 2,
            MaxCallsPerGame = Math.Max(1, budgetMaxCallsPerGame),
            DefaultTemperature = 0.7,
        }));
        services.AddSingleton(sp => new GameLlmBudget(
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.MaxCallsPerGame));
        services.AddSingleton<IAiCluePromptProviderFactory>(_ =>
            new TestInlinePromptProviderFactory("Français_OFF", null));
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IGenerateAICluesUseCase, GenerateAICluesPerDirection.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task HappyPath_four_calls_one_direction_each_emits_4_AiClueGenerated_and_auto_submits()
    {
        var fake = new FakeChatClient();
        var sp = BuildPerDirection(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);

        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top,    safe[0], "exp top") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  safe[1], "exp right") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[2], "exp bottom") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[3], "exp left") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(4, response.LlmCallsConsumed);

        var generated = events.PublishedEvents.OfType<AiClueGenerated>().ToList();
        Assert.Equal(4, generated.Count);

        var game = await repo.Get(gameId);
        Assert.True(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task Retry_one_direction_first_attempt_invalid_second_attempt_valid()
    {
        var fake = new FakeChatClient();
        var sp = BuildPerDirection(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);
        var conflict = PickConflictWord(board);

        // Top OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top, safe[0], "ok") });
        // Right : 1re tentative invalide (mot du board), 2e OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, conflict, "conflit") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, safe[1], "ok") });
        // Bottom + Left OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[2], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[3], "ok") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(5, response.LlmCallsConsumed); // 4 directions + 1 retry sur Right

        var generated = events.PublishedEvents.OfType<AiClueGenerated>().ToList();
        Assert.Equal(4, generated.Count);

        var game = await repo.Get(gameId);
        Assert.True(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    private static string PickConflictWord(CloverBoard board)
    {
        // Réutilise le pattern du test PerBoard : prend un mot apparaissant déjà sur le board pour forcer un rejet.
        return board.TopLeft!.GetWord(Direction.Top);
    }
}
