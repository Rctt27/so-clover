using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Games;
using Xunit;

namespace SoClover.Tests;

public class GameProcessManagerTimeoutTests
{
    private static string WwwrootPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot"));

    private ServiceProvider BuildProvider(TestClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

        var dictionaryPath = Path.Combine(WwwrootPath, "dictionaries");
        var settingsPath = Path.Combine(WwwrootPath, "game_settings.json");
        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(Path.GetFullPath(dictionaryPath)));

        var testClock = clock ?? new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(sp => testClock);
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider(Path.GetFullPath(settingsPath)));

        // Use cases used by GameProcessManager
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
        services.AddTransient<ICompleteGameUseCase, CompleteGame.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();

        // Background process manager that drives timeouts
        services.AddHostedService<GameProcessManager>();

        return services.BuildServiceProvider();
    }

    private static async Task StartHostedServicesAsync(ServiceProvider sp)
    {
        var hostedServices = sp.GetRequiredService<IEnumerable<IHostedService>>();
        foreach (var svc in hostedServices)
        {
            await svc.StartAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WritingClues_timeout_triggers_auto_start_guessing_via_process_manager()
    {
        using var sp = BuildProvider();
        await StartHostedServicesAsync(sp);

        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var clock = (TestClock)sp.GetRequiredService<IClock>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        await join.Handle(new JoinGame.Request(gameId, "Alice"));

        // Move to WritingClues (sets deadline)
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(GamePhase.WritingClues, game.Phase);
        var endsAt = game.PhaseEndsAtUtc!.Value;

        // Advance clock beyond deadline
        clock.Set(endsAt.AddSeconds(1));

        // Wait up to ~3 seconds for the process manager polling loop (1s delay) to process
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            if (game.Phase == GamePhase.Guessing)
                break;
            await Task.Delay(50);
        }

        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.NotNull(game.PhaseEndsAtUtc); // per-board deadline set
    }

    [Fact]
    public async Task Guessing_timeout_moves_to_next_board_and_eventually_enters_scoring()
    {
        using var sp = BuildProvider();
        await StartHostedServicesAsync(sp);

        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var clock = (TestClock)sp.GetRequiredService<IClock>();

        // Create game with two players
        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        await join.Handle(new JoinGame.Request(gameId, "Bob"));

        // Enter Writing then start Guessing (manual start is fine here; this test focuses on Guessing timeouts)
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        // Snapshot the first board deadline
        var game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(GamePhase.Guessing, game.Phase);
        var firstEnds = game.PhaseEndsAtUtc!.Value;

        // Expire the first board
        clock.Set(firstEnds.AddSeconds(1));

        // Wait for process manager to move to next board
        var sw1 = Stopwatch.StartNew();
        PlayerId? firstOwner = game.CurrentGuessingBoardOwner;
        while (sw1.Elapsed < TimeSpan.FromSeconds(3))
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            if (game.CurrentGuessingBoardOwner != firstOwner)
                break;
            await Task.Delay(50);
        }

        // Should have advanced to the next board and reset deadline
        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.NotEqual(firstOwner, game.CurrentGuessingBoardOwner);
        Assert.NotNull(game.PhaseEndsAtUtc);

        // Expire the last board as well -> should enter Scoring automatically
        var secondEnds = game.PhaseEndsAtUtc!.Value;
        clock.Set(secondEnds.AddSeconds(1));

        var sw2 = Stopwatch.StartNew();
        while (sw2.Elapsed < TimeSpan.FromSeconds(3))
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            if (game.Phase == GamePhase.Scoring)
                break;
            await Task.Delay(50);
        }

        Assert.Equal(GamePhase.Scoring, game.Phase);
        Assert.NotNull(game.PhaseEndsAtUtc); // scoring deadline is set by use case in current implementation
    }
}
