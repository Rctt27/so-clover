using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class FullGameFlowTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<ILeaveGameUseCase, LeaveGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IDisconnectPlayerUseCase, DisconnectPlayer.Handler>();
        services.AddTransient<IGuessUseCase, Guess.Handler>();
        services.AddTransient<IPlaceCardToGuessUseCase, PlaceCardToGuess.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        services.AddTransient<ICompleteGameUseCase, CompleteGame.Handler>();
        services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Full_game_flow_happy_path()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submitBoard = sp.GetRequiredService<ISubmitBoardUseCase>();
        var guess = sp.GetRequiredService<IGuessUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        // Create game with admin player
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;

        // Join 2 more players (admin is already in the game)
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        var p2 = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        // Writing phase auto-populates boards with 4 cards per player (unique words)
        var phase = (await startWriting.Handle(new StartWritingPhase.Request(gameId))).Phase;
        Assert.Equal(GamePhase.WritingClues, phase);

        // Set clues for each player
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top,    "Clue Admin1"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Right,  "Clue Admin2"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Bottom, "Clue Admin3"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Left,   "Clue Admin4"));

        await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Top,    "Clue A1"));
        await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Right,  "Clue A2"));
        await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Bottom, "Clue A3"));
        await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Left,   "Clue A4"));

        await setClue.Handle(new SetClue.Request(gameId, p2, Direction.Top,    "Clue B1"));
        await setClue.Handle(new SetClue.Request(gameId, p2, Direction.Right,  "Clue B2"));
        await setClue.Handle(new SetClue.Request(gameId, p2, Direction.Bottom, "Clue B3"));
        await setClue.Handle(new SetClue.Request(gameId, p2, Direction.Left,   "Clue B4"));

        // Submit all boards via use case — the last submit auto-transitions to Guessing.
        await submitBoard.Handle(new SubmitBoard.Request(gameId, adminId));
        await submitBoard.Handle(new SubmitBoard.Request(gameId, p1));
        await submitBoard.Handle(new SubmitBoard.Request(gameId, p2));

        Assert.Equal(GamePhase.Guessing, (await repo.Get(gameId))!.Phase);

        // Prepare expected clues by reading the game state
        var game = await repo.Get(gameId) ?? throw new Exception("Game not found in test");
        var players = game.Players.ToArray();

        // For each player's board, guess all four directions (wrong then correct)
        foreach (var player in players)
        {
            var expectedTop    = player.Board.GetClueText(Direction.Top);
            var expectedRight  = player.Board.GetClueText(Direction.Right);
            var expectedBottom = player.Board.GetClueText(Direction.Bottom);
            var expectedLeft   = player.Board.GetClueText(Direction.Left);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Top,    "wrong"))).IsCorrect);
            Assert.True( (await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Top,    expectedTop))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Right,  "wrong"))).IsCorrect);
            Assert.True( (await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Right,  expectedRight))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Bottom, "wrong"))).IsCorrect);
            Assert.True( (await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Bottom, expectedBottom))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Left,   "wrong"))).IsCorrect);
            Assert.True( (await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Left,   expectedLeft))).IsCorrect);
        }

        // The legacy Guess use case records answers but doesn't advance boards —
        // the game stays in Guessing until MoveToNextBoard is called explicitly.
        game = await repo.Get(gameId) ?? throw new Exception("Game not found in test");
        Assert.Equal(GamePhase.Guessing, game.Phase);
    }

    [Fact]
    public async Task Game_flow_with_player_disconnect_during_writing()
    {
        var sp = BuildProvider();

        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submitBoard = sp.GetRequiredService<ISubmitBoardUseCase>();
        var disconnect = sp.GetRequiredService<IDisconnectPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        // Create game with 3 players
        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        var bobId = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        // Start writing
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        // Alice disconnects
        await disconnect.Handle(new DisconnectPlayer.Request(gameId, aliceId));

        var game = await repo.Get(gameId);
        Assert.Equal(2, game!.ActivePlayers.Count);
        Assert.Equal(3, game.Players.Count); // Alice still in Players for scoring

        // Admin and Bob submit clues
        foreach (var pid in new[] { adminId, bobId })
        {
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Top, "c1"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Right, "c2"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Bottom, "c3"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Left, "c4"));
            await submitBoard.Handle(new SubmitBoard.Request(gameId, pid));
        }

        // Game should have auto-transitioned to Guessing (only 2 active players)
        game = await repo.Get(gameId);
        Assert.Equal(GamePhase.Guessing, game!.Phase);

        // Alice should already have a BoardResult as disconnected
        Assert.True(game.BoardResults.ContainsKey(aliceId));
        Assert.True(game.BoardResults[aliceId].IsDisconnected);
        Assert.False(game.BoardResults[aliceId].WasGuessed);
    }
}
