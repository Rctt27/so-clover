using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;
using SoClover.UseCases.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueOrchestratorEndToEndTests
{
    private static ServiceProvider BuildEndToEndProvider(
        IChatClient chatClient,
        int maxConcurrency = 4)
    {
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

        services.AddSingleton<IChatClient>(_ => new ThrottlingChatClient(chatClient, maxConcurrency));
        services.AddSingleton(Options.Create(new LlmOptions
        {
            MaxRetries = 2,
            MaxCallsPerGame = 100,
            MaxConcurrency = maxConcurrency,
        }));
        services.AddSingleton(sp => new GameLlmBudget(
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.MaxCallsPerGame));
        services.AddSingleton<IAiCluePromptProviderFactory>(_ =>
            new TestInlinePromptProviderFactory("Français_OFF", null));
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();
        services.AddSingleton<AiClueWorkChannel>();

        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IGenerateAICluesUseCase, GenerateAIClues.Handler>();

        return services.BuildServiceProvider();
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds:F1}s.");
    }

    private static string[] PickSafeClues(CloverBoard board, int count)
    {
        var validator = new FrenchOffClueValidator();
        var results = new List<string>();
        for (var i = 0; results.Count < count && i < 5000; i++)
        {
            var candidate = $"zzqxkj{i:D4}";
            var r = validator.Validate(candidate, Direction.Top, board);
            if (r.IsValid) results.Add(candidate);
        }
        return results.ToArray();
    }

    private static void EnqueueCluesForBoard(FakeChatClient fake, CloverBoard board, TimeSpan? delay = null)
    {
        var safe = PickSafeClues(board, 4);
        var json = JsonSerializer.Serialize(new
        {
            clues = new[]
            {
                new { direction = "Top",    clueWord = safe[0], explanation = "x" },
                new { direction = "Right",  clueWord = safe[1], explanation = "x" },
                new { direction = "Bottom", clueWord = safe[2], explanation = "x" },
                new { direction = "Left",   clueWord = safe[3], explanation = "x" },
            }
        });
        fake.Enqueue(json, delay);
    }

    [Fact]
    public async Task Cas1_2H_plus_1AI_admin_start_then_AI_board_auto_submits_without_human_action()
    {
        var fake = new FakeChatClient();
        var sp = BuildEndToEndProvider(fake);
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var channel = sp.GetRequiredService<AiClueWorkChannel>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var game = new Game(GameId.New(), "Français_OFF");
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        var service = new AiClueOrchestratorHostedService(scopeFactory, channel);
        using var cts = new CancellationTokenSource();

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var botBoard = (await repo.Get(game.Id))!.Players.First(p => p.Id == bot.Id).Board;
        EnqueueCluesForBoard(fake, botBoard);

        await service.StartAsync(cts.Token);

        await WaitForAsync(async () =>
        {
            var g = await repo.Get(game.Id);
            return g != null && g.Players.First(p => p.Id == bot.Id).Board.IsSubmitted;
        }, TimeSpan.FromSeconds(5));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        var finalGame = (await repo.Get(game.Id))!;
        var botFinal = finalGame.Players.First(p => p.Id == bot.Id);
        Assert.True(botFinal.Board.IsSubmitted);
        Assert.NotNull(botFinal.Board.TopClue);
        Assert.NotNull(botFinal.Board.RightClue);
        Assert.NotNull(botFinal.Board.BottomClue);
        Assert.NotNull(botFinal.Board.LeftClue);

        var events = sp.GetRequiredService<InMemoryEventPublisher>();
        Assert.Equal(1, events.PublishedEvents.OfType<AiClueGenerationRequested>().Count());
        Assert.Equal(4, events.PublishedEvents.OfType<AiClueGenerated>().Count());
        Assert.Single(events.PublishedEvents.OfType<BoardSubmitted>().Where(e => e.PlayerId == bot.Id));
    }
}
