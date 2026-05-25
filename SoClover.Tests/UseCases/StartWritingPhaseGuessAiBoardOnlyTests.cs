using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.RealTime;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class StartWritingPhaseGuessAiBoardOnlyTests
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
    public async Task StartWritingPhase_with_GuessAiBoardOnly_only_AI_get_cards()
    {
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var sp = BuildProvider(new FakeConnectionTracker(new[] { human.Id }));
        var repo = sp.GetRequiredService<IGameRepository>();

        var game = new Game(GameId.New());
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        await useCase.Handle(new StartWritingPhase.Request(game.Id));

        var reloaded = await repo.Get(game.Id);
        Assert.NotNull(reloaded);
        var humanState = reloaded!.Players.First(p => p.Id == human.Id);
        var botState = reloaded.Players.First(p => p.Id == bot.Id);

        Assert.Null(humanState.Board.TopLeft);
        Assert.Null(humanState.Board.TopRight);
        Assert.Null(humanState.Board.BottomRight);
        Assert.Null(humanState.Board.BottomLeft);

        Assert.NotNull(botState.Board.TopLeft);
        Assert.NotNull(botState.Board.TopRight);
        Assert.NotNull(botState.Board.BottomRight);
        Assert.NotNull(botState.Board.BottomLeft);
    }

    [Fact]
    public async Task StartWritingPhase_without_GuessAiBoardOnly_all_players_get_cards()
    {
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var sp = BuildProvider(new FakeConnectionTracker(new[] { human.Id }));
        var repo = sp.GetRequiredService<IGameRepository>();

        var game = new Game(GameId.New());
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        await useCase.Handle(new StartWritingPhase.Request(game.Id));

        var reloaded = await repo.Get(game.Id);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.Players.First(p => p.Id == human.Id).Board.TopLeft);
        Assert.NotNull(reloaded.Players.First(p => p.Id == bot.Id).Board.TopLeft);
    }
}
