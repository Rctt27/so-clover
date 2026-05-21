using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class UpdateGameSettingsGuessAiBoardOnlyTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<IUpdateGameSettingsUseCase, UpdateGameSettings.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task UpdateGameSettings_can_enable_GuessAiBoardOnly_when_AI_present()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bot);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IUpdateGameSettingsUseCase>();
        var response = await useCase.Handle(new UpdateGameSettings.Request(
            game.Id, admin.Id, game.Language, null, null, null, GuessAiBoardOnly: true));

        Assert.True(response.GuessAiBoardOnly);
        var reloaded = await repo.Get(game.Id);
        Assert.True(reloaded!.GuessAiBoardOnly);
    }

    [Fact]
    public async Task UpdateGameSettings_throws_when_enabling_GuessAiBoardOnly_without_AI()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        game.AddPlayer(admin);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IUpdateGameSettingsUseCase>();

        await Assert.ThrowsAsync<NoAiPlayerForGuessAiBoardOnlyException>(
            () => useCase.Handle(new UpdateGameSettings.Request(
                game.Id, admin.Id, game.Language, null, null, null, GuessAiBoardOnly: true)));
    }

    [Fact]
    public async Task UpdateGameSettings_null_GuessAiBoardOnly_leaves_flag_unchanged()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bot);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IUpdateGameSettingsUseCase>();
        var response = await useCase.Handle(new UpdateGameSettings.Request(
            game.Id, admin.Id, game.Language, null, null, null, GuessAiBoardOnly: null));

        Assert.True(response.GuessAiBoardOnly);
    }
}
