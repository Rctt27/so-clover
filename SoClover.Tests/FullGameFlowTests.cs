using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Boards;
using SoClover.UseCases.Games;
using Xunit;

namespace SoClover.Tests;

public class FullGameFlowTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot");
        var dictionaryPath = Path.Combine(wwwrootPath, "dictionaries");
        var settingsPath = Path.Combine(wwwrootPath, "game_settings.json");
        services.AddSingleton<IWordDictionary>(sp => 
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider(Path.GetFullPath(settingsPath)));
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGuessUseCase, Guess.Handler>();
        services.AddTransient<IPlaceCardToGuessUseCase, PlaceCardToGuess.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
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
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
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

        // Set clues (labels) for directions for each player
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

        // Guessing phase (force from test context)
        phase = (await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true))).Phase;
        Assert.Equal(GamePhase.Guessing, phase);

        // Prepare expected words by reading the game state
        var game = await repo.Get(gameId) ?? throw new Exception("Game not found in test");
        var players = game.Players.ToArray();

        // For each player, guess all four directions (simulate some wrong then right guesses)
        foreach (var player in players)
        {
            var expectedTop = player.Board.GetClueText(Direction.Top);
            var expectedRight = player.Board.GetClueText(Direction.Right);
            var expectedBottom = player.Board.GetClueText(Direction.Bottom);
            var expectedLeft = player.Board.GetClueText(Direction.Left);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Top, "wrong"))).IsCorrect);
            Assert.True((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Top, expectedTop))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Right, "wrong"))).IsCorrect);
            Assert.True((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Right, expectedRight))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Bottom, "wrong"))).IsCorrect);
            Assert.True((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Bottom, expectedBottom))).IsCorrect);

            Assert.False((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Left, "wrong"))).IsCorrect);
            Assert.True((await guess.Handle(new Guess.Request(gameId, player.Id, Direction.Left, expectedLeft))).IsCorrect);
        }

        // Game should be completed after all directions for all players are guessed
        game = await repo.Get(gameId) ?? throw new Exception("Game not found in test");
        Assert.Equal(GamePhase.Completed, game.Phase);
    }
}
