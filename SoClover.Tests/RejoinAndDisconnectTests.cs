using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SoClover.Tests;

public class RejoinAndDisconnectTests
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
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<ILeaveGameUseCase, LeaveGame.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task FindPlayerByName_returns_existing_player()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var response = await create.Handle(new CreateGame.Request("Alice"));
        var game = await repo.Get(response.GameId);

        var found = game!.FindPlayerByName("Alice");
        Assert.NotNull(found);
        Assert.Equal("Alice", found!.Name);
    }

    [Fact]
    public async Task FindPlayerByName_is_case_insensitive()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var response = await create.Handle(new CreateGame.Request("Alice"));
        var game = await repo.Get(response.GameId);

        Assert.NotNull(game!.FindPlayerByName("alice"));
        Assert.NotNull(game!.FindPlayerByName("ALICE"));
    }

    [Fact]
    public async Task FindPlayerByName_returns_null_when_not_found()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var response = await create.Handle(new CreateGame.Request("Alice"));
        var game = await repo.Get(response.GameId);

        Assert.Null(game!.FindPlayerByName("Bob"));
    }

    [Fact]
    public async Task ReplacePlayer_reuses_existing_playerId()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        var game = await repo.Get(gameId);
        Assert.Equal(2, game!.Players.Count);

        var newPlayer = new Player(PlayerId.New(), "Alice");
        var reusedId = game.ReplacePlayer(aliceId, newPlayer);

        Assert.Equal(aliceId, reusedId);
        Assert.Equal(2, game.Players.Count);
        Assert.Contains(game.Players, p => p.Id == aliceId);
    }

    [Fact]
    public void Player_can_be_marked_disconnected()
    {
        var player = new Player(PlayerId.New(), "Alice");
        Assert.False(player.IsDisconnected);

        player.MarkDisconnected();
        Assert.True(player.IsDisconnected);
    }

    [Fact]
    public async Task ActivePlayers_excludes_disconnected()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        var game = await repo.Get(gameId);
        Assert.Equal(2, game!.Players.Count);
        Assert.Equal(2, game.ActivePlayers.Count);

        var alice = game.Players.First(p => p.Id == aliceId);
        alice.MarkDisconnected();

        Assert.Equal(2, game.Players.Count);
        Assert.Equal(1, game.ActivePlayers.Count);
        Assert.DoesNotContain(game.ActivePlayers, p => p.Id == aliceId);
    }

}
