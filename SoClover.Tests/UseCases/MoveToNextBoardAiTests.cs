using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class MoveToNextBoardAiTests
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
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task MoveToNextBoard_transitions_to_Scoring_when_AI_is_last_board()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var afterWriting = (await repo.Get(game.Id))!;
        AiTestHelpers.SimulateAiBoardSubmit(afterWriting, bot.Id, DateTime.UtcNow);
        await repo.Save(afterWriting);

        await startGuessing.Handle(new StartGuessingPhase.Request(game.Id));

        var inGuessing = (await repo.Get(game.Id))!;
        Assert.Equal(1, inGuessing.BoardsToGuess.Count);

        var response = await moveNext.Handle(new MoveToNextBoard.Request(
            inGuessing.Id,
            inGuessing.CurrentGuessingBoardOwner!.Value,
            InvocationOrigin.System));

        Assert.Equal(GamePhase.Scoring, response.Phase);
    }

    [Fact]
    public async Task MoveToNextBoard_stays_in_Guessing_when_boards_remain()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot1 = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var bot2 = new Player(PlayerId.New(), "Bot-2", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddAIPlayer(bot1, max: 4);
        game.AddAIPlayer(bot2, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var afterWriting = (await repo.Get(game.Id))!;
        AiTestHelpers.SimulateAiBoardSubmit(afterWriting, bot1.Id, DateTime.UtcNow);
        AiTestHelpers.SimulateAiBoardSubmit(afterWriting, bot2.Id, DateTime.UtcNow);
        await repo.Save(afterWriting);

        await startGuessing.Handle(new StartGuessingPhase.Request(game.Id));

        var inGuessing = (await repo.Get(game.Id))!;
        Assert.Equal(2, inGuessing.BoardsToGuess.Count);

        var response = await moveNext.Handle(new MoveToNextBoard.Request(
            inGuessing.Id,
            inGuessing.CurrentGuessingBoardOwner!.Value,
            InvocationOrigin.System));

        Assert.Equal(GamePhase.Guessing, response.Phase);
    }
}
