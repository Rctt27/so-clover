using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SoClover.Tests;

public class LeaveGameTests
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
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Leave_game_removes_player_from_lobby()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var leave = sp.GetRequiredService<ILeaveGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        var game = await repo.Get(gameId);
        Assert.Equal(2, game!.Players.Count);

        await leave.Handle(new LeaveGame.Request(gameId, aliceId));

        game = await repo.Get(gameId);
        Assert.Equal(1, game!.Players.Count);
        Assert.DoesNotContain(game.Players, p => p.Id == aliceId);
    }

    [Fact]
    public async Task Leave_game_during_writing_phase_throws()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var leave = sp.GetRequiredService<ILeaveGameUseCase>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(() =>
            leave.Handle(new LeaveGame.Request(gameId, aliceId)));
    }

    [Fact]
    public async Task Leave_game_when_last_player_leaves()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var leave = sp.GetRequiredService<ILeaveGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;

        await leave.Handle(new LeaveGame.Request(gameId, adminId));

        var game = await repo.Get(gameId);
        Assert.Empty(game!.Players);
        Assert.Null(game.AdminPlayerId);
    }

    [Fact]
    public async Task Leave_game_reassigns_admin_when_admin_leaves()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var leave = sp.GetRequiredService<ILeaveGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;
        await join.Handle(new JoinGame.Request(gameId, "Alice"));

        await leave.Handle(new LeaveGame.Request(gameId, adminId));

        var game = await repo.Get(gameId);
        Assert.Equal(1, game!.Players.Count);
        Assert.True(game.Players.First().IsAdmin);
    }
}
