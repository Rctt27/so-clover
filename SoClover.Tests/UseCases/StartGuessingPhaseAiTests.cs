using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class StartGuessingPhaseAiTests
{
    private static ServiceProvider BuildProvider()
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
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Throws_NoHumanGuesserException_when_only_AI_remain()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var dict = sp.GetRequiredService<IWordDictionary>();

        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bot);
        await game.InitializeWordsPoolAsync(dict);
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        // Tous les boards submitted (humain + AI), mais l'humain est déconnecté → 0 humain pour deviner
        admin.Board.MarkSubmitted(DateTime.UtcNow);
        bot.Board.MarkSubmitted(DateTime.UtcNow);
        admin.MarkDisconnected();
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartGuessingPhaseUseCase>();

        await Assert.ThrowsAsync<NoHumanGuesserException>(
            () => useCase.Handle(new StartGuessingPhase.Request(game.Id)));
    }

    [Fact]
    public async Task Throws_NoHumanGuesserException_even_when_Force_is_true()
    {
        // Garantit que GameProcessManager (qui force=true au timeout) ne peut pas non plus
        // entrer en Guessing sans humain pour deviner.
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var dict = sp.GetRequiredService<IWordDictionary>();

        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bot);
        await game.InitializeWordsPoolAsync(dict);
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        bot.Board.MarkSubmitted(DateTime.UtcNow); // seul l'AI a submit
        admin.MarkDisconnected();
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartGuessingPhaseUseCase>();

        await Assert.ThrowsAsync<NoHumanGuesserException>(
            () => useCase.Handle(new StartGuessingPhase.Request(game.Id, Force: true)));
    }

}
