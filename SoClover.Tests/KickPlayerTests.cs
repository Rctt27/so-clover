using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SoClover.Tests;

public class KickPlayerTests
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
        services.Configure<GameDefaultsOptions>(_ => { });
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IKickPlayerUseCase, KickPlayer.Handler>();
        services.Configure<AIPlayersOptions>(o => o.Enabled = true);
        services.AddTransient<ICreateAIPlayerUseCase, CreateAIPlayer.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Admin_can_kick_player_from_lobby()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var kick = sp.GetRequiredService<IKickPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        var result = await kick.Handle(new KickPlayer.Request(gameId, aliceId, adminId));
        Assert.True(result.Success);

        var game = await repo.Get(gameId);
        Assert.Single(game!.Players);
        Assert.DoesNotContain(game.Players, p => p.Id == aliceId);
    }

    [Fact]
    public async Task Non_admin_cannot_kick()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var kick = sp.GetRequiredService<IKickPlayerUseCase>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        var bobId = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            kick.Handle(new KickPlayer.Request(gameId, bobId, aliceId)));
    }

    [Fact]
    public async Task Admin_cannot_kick_self()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var kick = sp.GetRequiredService<IKickPlayerUseCase>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kick.Handle(new KickPlayer.Request(gameId, adminId, adminId)));
    }

    [Fact]
    public async Task Admin_can_kick_AI_player_from_lobby()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var kick = sp.GetRequiredService<IKickPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResp = await create.Handle(new CreateGame.Request("Admin"));
        var botResp = await createAi.Handle(new CreateAIPlayer.Request(
            gameResp.GameId, gameResp.CreatorPlayerId, "Bot-1", "gpt-4o-mini", 0.7));

        var result = await kick.Handle(new KickPlayer.Request(
            gameResp.GameId, botResp.PlayerId, gameResp.CreatorPlayerId));
        Assert.True(result.Success);
        Assert.Equal("Bot-1", result.KickedPlayerName);

        var game = await repo.Get(gameResp.GameId);
        Assert.DoesNotContain(game!.Players, p => p.Id == botResp.PlayerId);
    }
}
