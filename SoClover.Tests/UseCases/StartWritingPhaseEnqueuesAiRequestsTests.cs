using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class StartWritingPhaseEnqueuesAiRequestsTests
{
    private static ServiceProvider BuildProvider(AiClueWorkChannel? channel)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(_ =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        if (channel != null) services.AddSingleton(channel);
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Enqueues_one_AiClueGenerationRequested_per_AI_player()
    {
        var channel = new AiClueWorkChannel();
        var sp = BuildProvider(channel);
        var repo = sp.GetRequiredService<IGameRepository>();

        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot1 = new Player(PlayerId.New(), "Bot1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var bot2 = new Player(PlayerId.New(), "Bot2", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var game = new Game(GameId.New(), "Français_OFF");
        game.AddPlayer(human);
        game.AddAIPlayer(bot1, max: 4);
        game.AddAIPlayer(bot2, max: 4);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        await useCase.Handle(new StartWritingPhase.Request(game.Id));

        var enqueued = new List<AiClueGenerationRequested>();
        while (channel.Reader.TryRead(out var msg)) enqueued.Add(msg);

        Assert.Equal(2, enqueued.Count);
        Assert.All(enqueued, m => Assert.Equal(game.Id, m.GameId));
        Assert.Contains(enqueued, m => m.PlayerId == bot1.Id);
        Assert.Contains(enqueued, m => m.PlayerId == bot2.Id);
    }

    [Fact]
    public async Task Enqueues_nothing_when_only_human_players()
    {
        var channel = new AiClueWorkChannel();
        var sp = BuildProvider(channel);
        var repo = sp.GetRequiredService<IGameRepository>();

        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var game = new Game(GameId.New(), "Français_OFF");
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        await useCase.Handle(new StartWritingPhase.Request(game.Id));

        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Does_not_throw_when_channel_not_registered_in_DI()
    {
        var sp = BuildProvider(channel: null);
        var repo = sp.GetRequiredService<IGameRepository>();

        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var game = new Game(GameId.New(), "Français_OFF");
        game.AddPlayer(human);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var response = await useCase.Handle(new StartWritingPhase.Request(game.Id));

        Assert.Equal(GamePhase.WritingClues, response.Phase);
    }
}
