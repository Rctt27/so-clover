using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class AiFullGameFlowTests
{
    private ServiceProvider BuildProvider()
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
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory,
            SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGuessUseCase, Guess.Handler>();
        services.AddTransient<IPlaceCardToGuessUseCase, PlaceCardToGuess.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Pose 4 clues valides pour un humain (passe par SetClue UseCase pour respecter la validation).
    /// </summary>
    private static async Task SetHumanCluesAsync(ISetClueUseCase setClue, GameId gameId, PlayerId pid, string prefix)
    {
        await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Top,    $"{prefix}-top"));
        await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Right,  $"{prefix}-right"));
        await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Bottom, $"{prefix}-bottom"));
        await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Left,   $"{prefix}-left"));
    }
    
    private sealed class FakeConnectionTracker : SoClover.RealTime.IConnectionTracker
    {
        private readonly HashSet<PlayerId> _connected;
        public FakeConnectionTracker(IEnumerable<PlayerId> connected) => _connected = new(connected);
        public bool IsPlayerConnected(PlayerId playerId) => _connected.Contains(playerId);
    }

    [Fact]
    public async Task Scenario_3_humans_plus_1_AI_reaches_Scoring_via_BoardsToGuess_cycle()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submit = sp.GetRequiredService<ISubmitBoardUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var game = new Game(GameId.New(), "Français_OFF");
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var carol = new Player(PlayerId.New(), "Carol");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bob);
        game.AddPlayer(carol);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        // 1) Simule submit AI EN PREMIER (Epic 06 le fera après génération LLM,
        // mais ici on s'assure juste que IsSubmitted=true avant le dernier submit humain).
        var withAi = await repo.Get(game.Id);
        Assert.NotNull(withAi);
        AiTestHelpers.SimulateAiBoardSubmit(withAi!, bot.Id, DateTime.UtcNow);
        await repo.Save(withAi!);

        // 2) Humains : clues + submit
        await SetHumanCluesAsync(setClue, game.Id, admin.Id, "admin");
        await SetHumanCluesAsync(setClue, game.Id, bob.Id,   "bob");
        await SetHumanCluesAsync(setClue, game.Id, carol.Id, "carol");

        await submit.Handle(new SubmitBoard.Request(game.Id, admin.Id));
        await submit.Handle(new SubmitBoard.Request(game.Id, bob.Id));
        await submit.Handle(new SubmitBoard.Request(game.Id, carol.Id));
        // Le dernier submit déclenche StartGuessingPhase auto.

        var inGuessing = await repo.Get(game.Id);
        Assert.NotNull(inGuessing);
        Assert.Equal(GamePhase.Guessing, inGuessing!.Phase);

        // 4 boards à deviner (3H + 1AI)
        Assert.Equal(4, inGuessing.BoardsToGuess.Count);
        // 3 humains pour deviner
        Assert.Equal(3, inGuessing.GuessingParticipants.Count);

        // 3) Cycle complet : 4 boards à épuiser (timeout = board incomplet).
        // Chaque board incomplet nécessite deux appels System :
        //   - 1er appel → cooldown (GuessingBoardRevealed=true), reste en Guessing.
        //   - 2e appel → avance au board suivant (ou Scoring si dernier board).
        for (int i = 0; i < 4; i++)
        {
            inGuessing = (await repo.Get(game.Id))!;
            // 1er appel : déclenche le cooldown.
            await moveNext.Handle(new MoveToNextBoard.Request(
                inGuessing.Id,
                inGuessing.CurrentGuessingBoardOwner!.Value,
                InvocationOrigin.System));
            inGuessing = (await repo.Get(game.Id))!;
            // 2e appel : efface le cooldown et avance réellement.
            await moveNext.Handle(new MoveToNextBoard.Request(
                inGuessing.Id,
                inGuessing.CurrentGuessingBoardOwner!.Value,
                InvocationOrigin.System));
            inGuessing = (await repo.Get(game.Id))!;
        }

        Assert.Equal(GamePhase.Scoring, inGuessing.Phase);
    }
    
    [Fact]
    public async Task Scenario_1_human_plus_2_AI_human_guesses_2_AI_boards_then_Scoring()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submit = sp.GetRequiredService<ISubmitBoardUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var game = new Game(GameId.New(), "Français_OFF");
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot1 = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var bot2 = new Player(PlayerId.New(), "Bot-2", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(alice);
        game.AddAIPlayer(bot1, max: 4);
        game.AddAIPlayer(bot2, max: 4);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        // AIs submit en premier
        var withAis = await repo.Get(game.Id);
        AiTestHelpers.SimulateAiBoardSubmit(withAis!, bot1.Id, DateTime.UtcNow);
        AiTestHelpers.SimulateAiBoardSubmit(withAis!, bot2.Id, DateTime.UtcNow);
        await repo.Save(withAis!);

        // Alice pose ses clues + submit (déclenche StartGuessingPhase auto)
        await SetHumanCluesAsync(setClue, game.Id, alice.Id, "alice");
        await submit.Handle(new SubmitBoard.Request(game.Id, alice.Id));

        var inGuessing = (await repo.Get(game.Id))!;
        Assert.Equal(GamePhase.Guessing, inGuessing.Phase);
        Assert.Equal(3, inGuessing.BoardsToGuess.Count); // Alice + 2 AI
        Assert.Single(inGuessing.GuessingParticipants);  // seule Alice devine

        // Chaque board incomplet nécessite deux appels System :
        //   - 1er appel → cooldown (GuessingBoardRevealed=true), reste en Guessing.
        //   - 2e appel → efface le cooldown et avance réellement.
        for (int i = 0; i < 3; i++)
        {
            inGuessing = (await repo.Get(game.Id))!;
            // 1er appel : déclenche le cooldown.
            await moveNext.Handle(new MoveToNextBoard.Request(
                inGuessing.Id, inGuessing.CurrentGuessingBoardOwner!.Value,
                InvocationOrigin.System));
            inGuessing = (await repo.Get(game.Id))!;
            // 2e appel : efface le cooldown et avance réellement.
            await moveNext.Handle(new MoveToNextBoard.Request(
                inGuessing.Id, inGuessing.CurrentGuessingBoardOwner!.Value,
                InvocationOrigin.System));
        }

        Assert.Equal(GamePhase.Scoring, (await repo.Get(game.Id))!.Phase);
    }
    
    [Fact]
    public async Task Scenario_2_humans_plus_1_AI_StartWritingPhase_succeeds_without_AI_connection()
    {
        // Ce scénario duplique partiellement Task 5 mais le valide bout-en-bout
        // (avec un IConnectionTracker enregistré dans le DI, ce qui n'est pas le cas
        // du builder par défaut). On enregistre un tracker fake qui connaît seulement les humains.
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

        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        // Le tracker connaît UNIQUEMENT les humains. Le bot doit être skip du check.
        var trackerFake = new FakeConnectionTracker(new[] { alice.Id, bob.Id });
        services.AddSingleton<SoClover.RealTime.IConnectionTracker>(trackerFake);

        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        var sp = services.BuildServiceProvider();
        var repo = sp.GetRequiredService<IGameRepository>();

        var game = new Game(GameId.New());
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var response = await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        Assert.Equal(GamePhase.WritingClues, response.Phase);
    }

    [Fact]
    public async Task Scenario_1_human_plus_1_AI_with_GuessAiBoardOnly_reaches_Guessing_without_human_submit()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var submit = sp.GetRequiredService<ISubmitBoardUseCase>();

        var game = new Game(GameId.New(), "Français_OFF");
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(alice);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        // Alice n'a PAS de cards en mode GuessAiBoardOnly.
        var afterStart = (await repo.Get(game.Id))!;
        var aliceState = afterStart.Players.First(p => p.Id == alice.Id);
        Assert.Null(aliceState.Board.TopLeft);

        // Tentative de submit pour Alice → doit échouer.
        await Assert.ThrowsAsync<HumanCannotSubmitInGuessAiBoardOnlyException>(
            () => submit.Handle(new SubmitBoard.Request(game.Id, alice.Id)));

        // Simuler la generation IA + submit (Epic 07 le fait en background).
        var withAi = (await repo.Get(game.Id))!;
        AiTestHelpers.SimulateAiBoardSubmit(withAi, bot.Id, DateTime.UtcNow);
        await repo.Save(withAi);

        // Le submit IA passe par le UseCase pour déclencher StartGuessingPhase.
        await submit.Handle(new SubmitBoard.Request(game.Id, bot.Id, InvocationOrigin.System));

        var finalState = (await repo.Get(game.Id))!;
        Assert.Equal(GamePhase.Guessing, finalState.Phase);
        Assert.Equal(1, finalState.BoardsToGuess.Count);
        Assert.Equal(1, finalState.GuessingParticipants.Count);
        Assert.Equal(bot.Id, finalState.CurrentGuessingBoardOwner);
    }
}