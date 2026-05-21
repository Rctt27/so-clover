using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class SubmitBoardGuessAiBoardOnlyTests
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
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SubmitBoard_human_throws_when_GuessAiBoardOnly_is_active()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var submit = sp.GetRequiredService<ISubmitBoardUseCase>();

        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        await Assert.ThrowsAsync<HumanCannotSubmitInGuessAiBoardOnlyException>(
            () => submit.Handle(new SubmitBoard.Request(game.Id, human.Id)));
    }
}
