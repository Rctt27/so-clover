using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Games;
using Xunit;

namespace SoClover.Tests;

public class GuessingTimeoutLastBoardTests
{
    private static string WwwrootPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot"));

    private ServiceProvider BuildProvider(TestClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

        var dictionaryPath = Path.Combine(WwwrootPath, "dictionaries");
        var settingsPath = Path.Combine(WwwrootPath, "game_settings.json");
        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(dictionaryPath));
        var testClock = clock ?? new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        services.AddSingleton<IClock>(sp => testClock);
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider(settingsPath));

        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task When_last_board_times_out_with_partial_placements_game_enters_scoring()
    {
        var sp = BuildProvider();
        var clock = (TestClock)sp.GetRequiredService<IClock>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();
        var placeGuessing = sp.GetRequiredService<IPlaceGuessingCardUseCase>();

        // Create game with 2 players (admin + one)
        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        var p2 = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        // Move to Writing and then start Guessing
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        // Reach the end of the first board by expiring time and invoking system move
        var state1 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state1.Phase);
        Assert.NotNull(state1.PhaseEndsAtUtc);
        // Advance clock to after deadline
        clock.Set(state1.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, state1.AdminPlayerId ?? default, InvocationOrigin.System));

        // We should now be on the last board (second player)
        var state2 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state2.Phase);
        Assert.NotNull(state2.GuessingState);
        Assert.NotNull(state2.PhaseEndsAtUtc);

        // Place one card on the board (as the non-owner player)
        var currentOwner = state2.GuessingState!.CurrentBoardOwnerId!.Value;
        var placerId = state2.Players.Select(p => p.PlayerId).First(p => p.Value != currentOwner.Value);
        // Place outside card index 0 on TopLeft
        await placeGuessing.Handle(new PlaceGuessingCard.Request(gameId, placerId, 0, BoardPosition.TopLeft));

        // Expire the timer on the last board and trigger system move
        state2 = await getState.Handle(new GetGameState.Request(gameId));
        clock.Set(state2.PhaseEndsAtUtc!.Value.AddSeconds(1));
        var response = await moveNext.Handle(new MoveToNextBoard.Request(gameId, placerId, InvocationOrigin.System));

        // Expect transition to Scoring
        Assert.Equal(GamePhase.Scoring, response.Phase);
        var state3 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Scoring, state3.Phase);
        // In the current implementation, the use case sets a scoring deadline when entering Scoring
        Assert.NotNull(state3.PhaseEndsAtUtc);
    }
}
