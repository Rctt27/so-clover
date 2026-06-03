using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GameProcessManagerTimeoutTests
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

        // Use cases used by GameProcessManager
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
        services.AddTransient<ICompleteGameUseCase, CompleteGame.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        services.AddSingleton(new SoClover.Infrastructure.AI.GameLlmBudget(maxCallsPerGame: 200));

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

    private static async Task WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds}s");
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

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(GamePhase.WritingClues, game.Phase);
        // Submit all boards so BoardsToGuess.Count > 0 when timeout forces Guessing (Epic 03 guard).
        game.SubmitAllBoards(clock);
        var endsAt = game.PhaseEndsAtUtc!.Value;

        clock.Set(endsAt.AddSeconds(1));

        await WaitForConditionAsync(async () =>
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            return game.Phase == GamePhase.Guessing;
        }, TimeSpan.FromSeconds(5));

        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.NotNull(game.PhaseEndsAtUtc);
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

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        await join.Handle(new JoinGame.Request(gameId, "Bob"));

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        preGuessing.SubmitAllBoards(clock);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        var game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(GamePhase.Guessing, game.Phase);
        var firstEnds = game.PhaseEndsAtUtc!.Value;

        clock.Set(firstEnds.AddSeconds(1));

        PlayerId? firstOwner = game.CurrentGuessingBoardOwner;
        // 1er timeout → cooldown de débrief (même owner, flag révélé), pas d'avance.
        await WaitForConditionAsync(async () =>
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            return game.GuessingBoardRevealed;
        }, TimeSpan.FromSeconds(5));
        Assert.Equal(firstOwner, game.CurrentGuessingBoardOwner);

        // Fin du cooldown → avance au board suivant.
        clock.Set(game.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await WaitForConditionAsync(async () =>
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            return game.CurrentGuessingBoardOwner != firstOwner;
        }, TimeSpan.FromSeconds(5));

        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.NotEqual(firstOwner, game.CurrentGuessingBoardOwner);
        Assert.NotNull(game.PhaseEndsAtUtc);

        // 2e board : même mécanique — 1er timeout → cooldown, 2e timeout → Scoring.
        var secondOwner = game.CurrentGuessingBoardOwner;
        var secondEnds = game.PhaseEndsAtUtc!.Value;
        clock.Set(secondEnds.AddSeconds(1));

        // 1er timeout sur le 2e board → cooldown de débrief.
        await WaitForConditionAsync(async () =>
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            return game.GuessingBoardRevealed;
        }, TimeSpan.FromSeconds(5));
        Assert.Equal(secondOwner, game.CurrentGuessingBoardOwner);

        // Fin du cooldown → Scoring.
        clock.Set(game.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await WaitForConditionAsync(async () =>
        {
            game = await repo.Get(gameId) ?? throw new Exception();
            return game.Phase == GamePhase.Scoring;
        }, TimeSpan.FromSeconds(5));

        Assert.Equal(GamePhase.Scoring, game.Phase);
        Assert.NotNull(game.PhaseEndsAtUtc);
    }
}
