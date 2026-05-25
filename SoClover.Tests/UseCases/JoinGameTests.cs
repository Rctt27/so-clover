using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SoClover.Tests.UseCases;

public class JoinGameTests
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
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task JoinGame_with_existing_name_without_replace_returns_conflict()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();

        var gameResponse = await create.Handle(new CreateGame.Request("Alice"));
        var gameId = gameResponse.GameId;

        var response = await join.Handle(new JoinGame.Request(gameId, "Alice", ReplaceExisting: false));
        Assert.True(response.IsConflict);
        Assert.Equal(gameResponse.CreatorPlayerId, response.ExistingPlayerId);
    }

    [Fact]
    public async Task JoinGame_with_existing_name_and_replace_reuses_id()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Alice"));
        var gameId = gameResponse.GameId;
        var originalId = gameResponse.CreatorPlayerId;

        var response = await join.Handle(new JoinGame.Request(gameId, "Alice", ReplaceExisting: true));
        Assert.False(response.IsConflict);
        Assert.Equal(originalId, response.PlayerId);

        var game = await repo.Get(gameId);
        Assert.Single(game!.Players);
    }
}
