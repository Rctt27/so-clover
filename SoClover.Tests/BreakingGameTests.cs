using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Boards;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Games;
using Xunit;

namespace SoClover.Tests;

public class BreakingGameTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        services.AddSingleton<IWordDictionary, InMemoryWordDictionary>();
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGuessUseCase, Guess.Handler>();
        services.AddTransient<IPlaceCardUseCase, PlaceCard.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartWriting_without_players_throws_NotEnoughPlayers()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;

        await Assert.ThrowsAsync<NotEnoughPlayersException>(async () =>
            await startWriting.Handle(new StartWritingPhase.Request(gameId)));
    }

    [Fact]
    public async Task Join_after_start_writing_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;

        // First player joins, then writing starts
        await join.Handle(new JoinGame.Request(gameId, "Alice"));
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await join.Handle(new JoinGame.Request(gameId, "Bob")));
    }

    [Fact]
    public async Task StartGuessing_from_lobby_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await startGuessing.Handle(new StartGuessingPhase.Request(gameId)));
    }

    [Fact]
    public async Task SetClue_outside_writing_phase_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        // Still in Lobby
        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Top, "X")));
    }

    [Fact]
    public async Task Guess_before_guessing_phase_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var guess = sp.GetRequiredService<IGuessUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId)); // still WritingClues

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await guess.Handle(new Guess.Request(gameId, p1, Direction.Top, "whatever")));
    }

    [Fact]
    public async Task SetClue_with_invalid_text_throws_InvalidClueException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await Assert.ThrowsAsync<InvalidClueException>(async () =>
            await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Top, "")));

        var longText = new string('a', 33);
        await Assert.ThrowsAsync<InvalidClueException>(async () =>
            await setClue.Handle(new SetClue.Request(gameId, p1, Direction.Top, longText)));
    }

    [Fact]
    public async Task PlaceCard_with_invalid_words_throws_CardWord_exceptions()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var place = sp.GetRequiredService<IPlaceCardUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        await Assert.ThrowsAsync<CardWordEmptyException>(async () =>
            await place.Handle(new PlaceCard.Request(gameId, p1, BoardPosition.TopLeft, "", "b", "c", "d")));

        var longWord = new string('x', 33);
        await Assert.ThrowsAsync<CardWordTooLongException>(async () =>
            await place.Handle(new PlaceCard.Request(gameId, p1, BoardPosition.TopLeft, longWord, "b", "c", "d")));
    }

    [Fact]
    public async Task Join_with_invalid_player_name_throws_specific_exceptions()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;

        await Assert.ThrowsAsync<PlayerNameEmptyException>(async () =>
            await join.Handle(new JoinGame.Request(gameId, " ")));

        var longName = new string('n', 33);
        await Assert.ThrowsAsync<PlayerNameTooLongException>(async () =>
            await join.Handle(new JoinGame.Request(gameId, longName)));
    }

    [Fact]
    public async Task Guess_with_unknown_player_throws_PlayerNotFound()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var guess = sp.GetRequiredService<IGuessUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request())).GameId;
        await join.Handle(new JoinGame.Request(gameId, "Alice"));
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId));

        await Assert.ThrowsAsync<PlayerNotFoundException>(async () =>
            await guess.Handle(new Guess.Request(gameId, PlayerId.New(), Direction.Top, "x")));
    }

    [Fact]
    public async Task UseCases_with_unknown_game_throw_GameNotFound()
    {
        var sp = BuildProvider();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var unknown = GameId.New();
        await Assert.ThrowsAsync<GameNotFoundException>(async () =>
            await join.Handle(new JoinGame.Request(unknown, "Alice")));
        await Assert.ThrowsAsync<GameNotFoundException>(async () =>
            await startWriting.Handle(new StartWritingPhase.Request(unknown)));
    }
}
