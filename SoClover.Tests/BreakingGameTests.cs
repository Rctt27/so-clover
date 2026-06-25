using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class BreakingGameTests
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
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGuessUseCase, Guess.Handler>();
        services.AddTransient<IPlaceCardToGuessUseCase, PlaceCardToGuess.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartWriting_without_players_throws_NotEnoughPlayers()
    {
        var sp = BuildProvider();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        // CreateGame.Handler ajoute automatiquement l'admin comme premier joueur,
        // ce qui rend le cas "0 joueur" inatteignable via le use case normal.
        // On instancie Game directement pour tester ce guard de domaine.
        var emptyGame = new Game(GameId.New());
        await emptyGame.InitializeWordsPoolAsync(sp.GetRequiredService<IWordDictionary>());
        await repo.Save(emptyGame);

        await Assert.ThrowsAsync<NotEnoughPlayersException>(async () =>
            await startWriting.Handle(new StartWritingPhase.Request(emptyGame.Id)));
    }

    [Fact]
    public async Task Join_after_start_writing_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request("Admin"))).GameId;

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
        var gameId = (await create.Handle(new CreateGame.Request("Admin"))).GameId;

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await startGuessing.Handle(new StartGuessingPhase.Request(gameId)));
    }

    [Fact]
    public async Task SetClue_outside_writing_phase_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;

        // Still in Lobby
        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, "X")));
    }

    [Fact]
    public async Task Guess_before_guessing_phase_throws_InvalidOperationInPhase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var guess = sp.GetRequiredService<IGuessUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(async () =>
            await guess.Handle(new Guess.Request(gameId, adminId, Direction.Top, "whatever")));
    }

    [Fact]
    public async Task SetClue_with_empty_text_throws_InvalidClueException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await Assert.ThrowsAsync<InvalidClueException>(async () =>
            await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, "")));
    }

    [Fact]
    public async Task SetClue_with_text_too_long_returns_TooLong_rejection()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        var longText = new string('a', Game.MaxClueLength + 1);
        var resp = await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, longText));

        Assert.False(resp.Validation.IsValid);
        Assert.Contains(resp.Validation.Errors, e => e.Rule == ClueValidationRule.TooLong);
        Assert.Contains(resp.Validation.Errors, e => e.MaxLength == Game.MaxClueLength);
    }

    [Fact]
    public async Task SetClue_at_max_length_is_accepted()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        var maxText = new string('z', Game.MaxClueLength);
        var resp = await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, maxText));

        Assert.True(resp.Validation.IsValid);
    }

    [Fact]
    public async Task PlaceCard_with_empty_word_throws_CardWordEmptyException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var place = sp.GetRequiredService<IPlaceCardToGuessUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;

        await Assert.ThrowsAsync<CardWordEmptyException>(async () =>
            await place.Handle(new PlaceCardToGuess.Request(gameId, adminId, BoardPosition.TopLeft, "", "b", "c", "d")));
    }

    [Fact]
    public async Task PlaceCard_with_word_too_long_throws_CardWordTooLongException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var place = sp.GetRequiredService<IPlaceCardToGuessUseCase>();
        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;

        var longWord = new string('x', 33);
        await Assert.ThrowsAsync<CardWordTooLongException>(async () =>
            await place.Handle(new PlaceCardToGuess.Request(gameId, adminId, BoardPosition.TopLeft, longWord, "b", "c", "d")));
    }

    [Fact]
    public async Task Join_with_empty_name_throws_PlayerNameEmptyException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request("Admin"))).GameId;

        await Assert.ThrowsAsync<PlayerNameEmptyException>(async () =>
            await join.Handle(new JoinGame.Request(gameId, " ")));
    }

    [Fact]
    public async Task Join_with_name_too_long_throws_PlayerNameTooLongException()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var gameId = (await create.Handle(new CreateGame.Request("Admin"))).GameId;

        var longName = new string('n', 33);
        await Assert.ThrowsAsync<PlayerNameTooLongException>(async () =>
            await join.Handle(new JoinGame.Request(gameId, longName)));
    }

    [Fact]
    public async Task Guess_with_unknown_player_throws_PlayerNotFound()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var guess = sp.GetRequiredService<IGuessUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var gameId = (await create.Handle(new CreateGame.Request("Admin"))).GameId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        await Assert.ThrowsAsync<PlayerNotFoundException>(async () =>
            await guess.Handle(new Guess.Request(gameId, PlayerId.New(), Direction.Top, "x")));
    }

    [Fact]
    public async Task JoinGame_with_unknown_game_throws_GameNotFoundException()
    {
        var sp = BuildProvider();
        var join = sp.GetRequiredService<IJoinGameUseCase>();

        await Assert.ThrowsAsync<GameNotFoundException>(async () =>
            await join.Handle(new JoinGame.Request(GameId.New(), "Alice")));
    }

    [Fact]
    public async Task StartWritingPhase_with_unknown_game_throws_GameNotFoundException()
    {
        var sp = BuildProvider();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        await Assert.ThrowsAsync<GameNotFoundException>(async () =>
            await startWriting.Handle(new StartWritingPhase.Request(GameId.New())));
    }

    [Fact]
    public async Task StartGuessing_with_Force_throws_NotEnoughPlayers_when_no_board_submitted()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var dict = sp.GetRequiredService<IWordDictionary>();

        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(alice);
        await game.InitializeWordsPoolAsync(dict);
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartGuessingPhaseUseCase>();

        var ex = await Assert.ThrowsAsync<NotEnoughPlayersException>(
            () => useCase.Handle(new StartGuessingPhase.Request(game.Id, Force: true)));
        Assert.Equal(1, ex.RequiredMinimum);
        Assert.Equal(0, ex.ActualCount);
    }
}
