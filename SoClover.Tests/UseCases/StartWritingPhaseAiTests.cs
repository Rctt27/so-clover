using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.RealTime;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class StartWritingPhaseAiTests
{
    private static ServiceProvider BuildProvider(IConnectionTracker tracker)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton(tracker);
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartWritingPhase_succeeds_when_AI_player_has_no_SignalR_connection()
    {
        var humanA = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var humanB = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        var tracker = new FakeConnectionTracker(new[] { humanA.Id, humanB.Id });
        var sp = BuildProvider(tracker);

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = new Game(GameId.New());
        game.AddPlayer(humanA);
        game.AddPlayer(humanB);
        game.AddPlayer(bot);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var response = await useCase.Handle(new StartWritingPhase.Request(game.Id));

        Assert.Equal(GamePhase.WritingClues, response.Phase);
    }

    [Fact]
    public async Task StartWritingPhase_throws_when_HUMAN_player_is_disconnected_even_with_AI_present()
    {
        var humanA = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var humanB = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        var tracker = new FakeConnectionTracker(new[] { humanA.Id });
        var sp = BuildProvider(tracker);

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = new Game(GameId.New());
        game.AddPlayer(humanA);
        game.AddPlayer(humanB);
        game.AddPlayer(bot);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var ex = await Assert.ThrowsAsync<DisconnectedPlayersException>(
            () => useCase.Handle(new StartWritingPhase.Request(game.Id)));
        Assert.Contains("Bob", ex.PlayerNames);
        Assert.DoesNotContain("Bot-1", ex.PlayerNames);
    }
}
