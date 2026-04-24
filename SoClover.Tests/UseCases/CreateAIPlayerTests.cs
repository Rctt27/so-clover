using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace SoClover.Tests.UseCases;

public class CreateAIPlayerTests
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
        services.Configure<GameDefaultsOptions>(opts => { });
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<ICreateAIPlayerUseCase, CreateAIPlayer.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Admin_can_create_AI_player_with_explicit_config()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        var resp = await createAi.Handle(new CreateAIPlayer.Request(
            game.GameId, game.CreatorPlayerId, "Bot-1", "gpt-4o-mini", 0.7));

        var stored = await repo.Get(game.GameId);
        var bot = stored!.Players.FirstOrDefault(p => p.Id == resp.PlayerId);
        Assert.NotNull(bot);
        Assert.True(bot!.IsAI);
        Assert.Equal("Bot-1", bot.Name);
        Assert.NotNull(bot.AIConfig);
        Assert.Equal("gpt-4o-mini", bot.AIConfig!.Model);
        Assert.Equal(0.7, bot.AIConfig.Temperature);
    }

    [Fact]
    public async Task AI_player_AIConfig_is_null_when_model_and_temperature_omitted()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        var resp = await createAi.Handle(new CreateAIPlayer.Request(
            game.GameId, game.CreatorPlayerId, "Bot-default", null, null));

        var stored = await repo.Get(game.GameId);
        var bot = stored!.Players.First(p => p.Id == resp.PlayerId);
        Assert.True(bot.IsAI);
        Assert.Null(bot.AIConfig);
    }

    [Fact]
    public async Task Non_admin_cannot_create_AI_player()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        var aliceId = (await join.Handle(new JoinGame.Request(game.GameId, "Alice"))).PlayerId;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            createAi.Handle(new CreateAIPlayer.Request(
                game.GameId, aliceId, "Bot-1", "gpt-4o-mini", 0.7)));
    }

    [Fact]
    public async Task Cap_is_enforced_at_MaxAIPlayersPerGame()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var settings = sp.GetRequiredService<IGameSettingsProvider>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        var max = (await settings.GetAsync()).MaxAIPlayersPerGame;

        for (int i = 0; i < max; i++)
        {
            await createAi.Handle(new CreateAIPlayer.Request(
                game.GameId, game.CreatorPlayerId, $"Bot-{i}", "gpt-4o-mini", 0.7));
        }

        var ex = await Assert.ThrowsAsync<MaxAIPlayersReachedException>(() =>
            createAi.Handle(new CreateAIPlayer.Request(
                game.GameId, game.CreatorPlayerId, "Bot-extra", "gpt-4o-mini", 0.7)));
        Assert.Equal(max, ex.CurrentCount);
        Assert.Equal(max, ex.Max);
    }

    [Fact]
    public async Task Cannot_create_AI_player_outside_Lobby_phase()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        await join.Handle(new JoinGame.Request(game.GameId, "Alice"));
        await startWriting.Handle(new StartWritingPhase.Request(game.GameId));

        await Assert.ThrowsAsync<InvalidOperationInPhaseException>(() =>
            createAi.Handle(new CreateAIPlayer.Request(
                game.GameId, game.CreatorPlayerId, "Bot-late", "gpt-4o-mini", 0.7)));
    }
}
