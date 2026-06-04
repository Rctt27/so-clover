using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SoClover.Tests;

public class ReconnectPlayerTests
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
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IDisconnectPlayerUseCase, DisconnectPlayer.Handler>();
        services.AddTransient<IReconnectPlayerUseCase, ReconnectPlayer.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Player_can_be_reconnected_after_disconnect()
    {
        var player = new Player(PlayerId.New(), "Alice");
        player.MarkDisconnected();
        Assert.True(player.IsDisconnected);

        player.MarkReconnected();
        Assert.False(player.IsDisconnected);
    }

    [Fact]
    public async Task ReconnectPlayer_reactivates_and_clears_disconnect_board_result_in_writing()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var disconnect = sp.GetRequiredService<IDisconnectPlayerUseCase>();
        var reconnect = sp.GetRequiredService<IReconnectPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await disconnect.Handle(new DisconnectPlayer.Request(gameId, aliceId));
        var afterDisconnect = await repo.Get(gameId);
        Assert.True(afterDisconnect!.Players.First(p => p.Id == aliceId).IsDisconnected);
        Assert.True(afterDisconnect.BoardResults.ContainsKey(aliceId));

        var result = await reconnect.Handle(new ReconnectPlayer.Request(gameId, aliceId));
        Assert.True(result.Reactivated);

        var afterReconnect = await repo.Get(gameId);
        Assert.False(afterReconnect!.Players.First(p => p.Id == aliceId).IsDisconnected);
        Assert.False(afterReconnect.BoardResults.ContainsKey(aliceId));
        Assert.Contains(afterReconnect.ActivePlayers, p => p.Id == aliceId);
    }

    [Fact]
    public async Task ReconnectPlayer_is_noop_when_player_not_disconnected()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var reconnect = sp.GetRequiredService<IReconnectPlayerUseCase>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        var result = await reconnect.Handle(new ReconnectPlayer.Request(gameId, aliceId));
        Assert.False(result.Reactivated);
    }

    [Fact]
    public void Game_ReconnectPlayer_does_not_reactivate_outside_writing()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(alice);
        alice.MarkDisconnected();

        var reactivated = game.ReconnectPlayer(alice.Id);

        Assert.False(reactivated);
        Assert.True(game.Players.First(p => p.Id == alice.Id).IsDisconnected);
    }
}
