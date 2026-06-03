using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GuessingCooldownTests
{
    private static string DictionariesPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

    private static ServiceProvider BuildProvider(TestClock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        services.AddSingleton<IWordDictionary>(_ => new FileWordDictionary(DictionariesPath));
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
        services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        return services.BuildServiceProvider();
    }

    private static async Task<(GameId gameId, IGameRepository repo, ServiceProvider sp)> BuildGuessingGame(TestClock clock)
    {
        var sp = BuildProvider(clock);
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        await join.Handle(new JoinGame.Request(gameId, "Bob"));
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var pre = await repo.Get(gameId) ?? throw new Exception();
        pre.SubmitAllBoards(clock);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));
        return (gameId, repo, sp);
    }

    [Fact]
    public async Task StartGuessingCooldown_sets_flag_and_deadline_and_is_idempotent()
    {
        var clock = new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (gameId, repo, sp) = await BuildGuessingGame(clock);
        using var _ = sp;

        var game = await repo.Get(gameId) ?? throw new Exception();
        Assert.False(game.GuessingBoardRevealed);
        var now = clock.UtcNow;

        game.StartGuessingCooldown(now, TimeSpan.FromSeconds(60));

        Assert.True(game.GuessingBoardRevealed);
        Assert.Equal(now.AddSeconds(60), game.PhaseEndsAtUtc);

        // Idempotent : un second appel (deadline différente) ne réécrit pas.
        game.StartGuessingCooldown(now.AddSeconds(10), TimeSpan.FromSeconds(60));
        Assert.Equal(now.AddSeconds(60), game.PhaseEndsAtUtc);
    }
}
