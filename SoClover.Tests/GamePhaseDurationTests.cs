using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GamePhaseDurationTests
{
    private static string DictionariesPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

    private ServiceProvider BuildProvider(TestClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(DictionariesPath));
        var testClock = clock ?? new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(sp => testClock);
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();

        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task WritingClues_and_Guessing_expose_non_null_deadline_within_1800s()
    {
        using var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var clock = (TestClock)sp.GetRequiredService<IClock>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        await join.Handle(new JoinGame.Request(gameId, "Alice"));
        await join.Handle(new JoinGame.Request(gameId, "Bob"));

        var beforeStart = clock.UtcNow;
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var state1 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.WritingClues, state1.Phase);
        Assert.NotNull(state1.PhaseEndsAtUtc);
        Assert.InRange((state1.PhaseEndsAtUtc!.Value - beforeStart).TotalSeconds, 1, 1800);

        clock.Advance(TimeSpan.FromSeconds(1));
        var repo = sp.GetRequiredService<IGameRepository>();
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        preGuessing.SubmitAllBoards(clock);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));
        var state2 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state2.Phase);
        Assert.NotNull(state2.PhaseEndsAtUtc);
        Assert.InRange((state2.PhaseEndsAtUtc!.Value - clock.UtcNow).TotalSeconds, 1, 1800);
    }
}
