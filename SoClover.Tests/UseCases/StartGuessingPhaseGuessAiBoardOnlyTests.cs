using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class StartGuessingPhaseGuessAiBoardOnlyTests
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
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartGuessingPhase_succeeds_when_only_AI_submitted_in_GuessAiBoardOnly_mode()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();

        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var withAi = (await repo.Get(game.Id))!;
        AiTestHelpers.SimulateAiBoardSubmit(withAi, bot.Id, DateTime.UtcNow);
        await repo.Save(withAi);

        var response = await startGuessing.Handle(new StartGuessingPhase.Request(game.Id));

        Assert.Equal(GamePhase.Guessing, response.Phase);
        var finalState = (await repo.Get(game.Id))!;
        Assert.Equal(1, finalState.BoardsToGuess.Count);
        Assert.Equal(1, finalState.GuessingParticipants.Count);
        Assert.Equal(bot.Id, finalState.CurrentGuessingBoardOwner);
    }
}
