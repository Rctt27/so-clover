using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class ScoringLogicTests
{
    private static string DictionariesPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

    private ServiceProvider BuildProvider(TestClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(DictionariesPath));
        var testClock = clock ?? new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(sp => testClock);
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();

        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
        services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();
        services.AddTransient<IGetScoringUseCase, GetScoring.Handler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Scoring_records_both_boards_as_failed_when_guessing_attempts_exhausted()
    {
        var sp = BuildProvider();
        var clock = (TestClock)sp.GetRequiredService<IClock>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var validate = sp.GetRequiredService<IValidateGuessingBoardUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();
        var placeCard = sp.GetRequiredService<IPlaceGuessingCardUseCase>();
        var getScoring = sp.GetRequiredService<IGetScoringUseCase>();

        // 1. Setup game with 2 players
        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        var p1Id = created.CreatorPlayerId;
        var p2Id = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        // Submit all boards so BoardsToGuess.Count > 0 (Epic 03 guard).
        var repo = sp.GetRequiredService<IGameRepository>();
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        var state = await getState.Handle(new GetGameState.Request(gameId));
        var owner1Id = new PlayerId(state.GuessingState!.CurrentBoardOwnerId!.Value);
        var guesser1Id = state.Players.First(p => p.PlayerId != owner1Id.Value).PlayerId;
        var guesser1PlayerId = new PlayerId(guesser1Id);

        // 2. Player 1 board: Failure (exhaust attempts)
        // Re-fetch game to have latest state
        var gameInstance = await sp.GetRequiredService<IGameRepository>().Get(gameId);
        
        // Fill the board with whatever is in the pool
        for (int i = 0; i < 4; i++) {
            await placeCard.Handle(new PlaceGuessingCard.Request(gameId, guesser1PlayerId, i, (BoardPosition)i));
        }

        // Validate until failure (3 attempts)
        for (int i = 0; i < 3; i++) {
             // Re-fill missing cards (incorrect ones are returned to pool)
             gameInstance = await sp.GetRequiredService<IGameRepository>().Get(gameId);
             for (int j = 0; j < 4; j++) {
                 if (gameInstance!.GuessedCardPositions[(BoardPosition)j] == null) {
                     // Find first non-null in OutsideCards
                     int poolIdx = gameInstance.OutsideCards.FindIndex(c => c != null);
                     if (poolIdx != -1)
                        await placeCard.Handle(new PlaceGuessingCard.Request(gameId, guesser1PlayerId, poolIdx, (BoardPosition)j));
                 }
             }
             await validate.Handle(new ValidateGuessingBoard.Request(gameId, guesser1PlayerId));
        }
        
        // Move to next
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, guesser1PlayerId));

        // 3. Player 2 board: Failure (exhaust attempts)
        state = await getState.Handle(new GetGameState.Request(gameId));
        var owner2Id = new PlayerId(state.GuessingState!.CurrentBoardOwnerId!.Value);
        var guesser2Id = state.Players.First(p => p.PlayerId != owner2Id.Value).PlayerId;
        var guesser2PlayerId = new PlayerId(guesser2Id);

        // Fill the board
        for (int i = 0; i < 4; i++) {
            await placeCard.Handle(new PlaceGuessingCard.Request(gameId, guesser2PlayerId, i, (BoardPosition)i));
        }

        // Validate until failure (3 attempts)
        for (int i = 0; i < 3; i++) {
             gameInstance = await sp.GetRequiredService<IGameRepository>().Get(gameId);
             for (int j = 0; j < 4; j++) {
                 if (gameInstance!.GuessedCardPositions[(BoardPosition)j] == null) {
                     int poolIdx = gameInstance.OutsideCards.FindIndex(c => c != null);
                     if (poolIdx != -1)
                        await placeCard.Handle(new PlaceGuessingCard.Request(gameId, guesser2PlayerId, poolIdx, (BoardPosition)j));
                 }
             }
             await validate.Handle(new ValidateGuessingBoard.Request(gameId, guesser2PlayerId));
        }

        // Move to next (Scoring phase)
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, guesser2PlayerId));

        // 4. Check Scoring
        var scoring = await getScoring.Handle(new GetScoring.Request(gameId));
        
        Assert.Equal(2, scoring.FailedBoards.Count);
        Assert.Empty(scoring.SuccessfulBoards);
    }
}
