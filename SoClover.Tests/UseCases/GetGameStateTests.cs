using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class GetGameStateTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(_ => new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.Configure<GameDefaultsOptions>(_ => { });
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.Configure<AIPlayersOptions>(o => o.Enabled = true);
        services.AddTransient<ICreateAIPlayerUseCase, CreateAIPlayer.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetGameState_exposes_isAI_true_for_AI_players_and_false_for_humans()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var createAi = sp.GetRequiredService<ICreateAIPlayerUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();

        var game = await create.Handle(new CreateGame.Request("Admin"));
        var bot = await createAi.Handle(new CreateAIPlayer.Request(
            game.GameId, game.CreatorPlayerId, "Bot-1", "gpt-4o-mini", 0.7));

        var state = await getState.Handle(new GetGameState.Request(game.GameId));
        var admin = state.Players.First(p => p.PlayerId == game.CreatorPlayerId.Value);
        var ai = state.Players.First(p => p.PlayerId == bot.PlayerId.Value);

        Assert.False(admin.IsAI);
        Assert.True(ai.IsAI);
    }
}
