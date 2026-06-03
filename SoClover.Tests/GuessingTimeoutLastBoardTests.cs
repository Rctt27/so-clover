using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GuessingTimeoutLastBoardTests
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
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task When_last_board_times_out_with_partial_placements_game_enters_scoring()
    {
        using var sp = BuildProvider();
        var clock = (TestClock)sp.GetRequiredService<IClock>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();
        var placeGuessing = sp.GetRequiredService<IPlaceGuessingCardUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        var p2 = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        preGuessing.SubmitAllBoards(clock);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));

        // Expire the first board via system move — board is incomplete, so this starts cooldown.
        var state1 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state1.Phase);
        Assert.NotNull(state1.PhaseEndsAtUtc);
        clock.Set(state1.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, new PlayerId(state1.AdminPlayerId ?? default), InvocationOrigin.System));

        // 1er timeout → cooldown actif, owner inchangé.
        var cooldown1 = await repo.Get(gameId) ?? throw new Exception();
        Assert.True(cooldown1.GuessingBoardRevealed);

        // Fin du cooldown → avance au board suivant.
        clock.Set(cooldown1.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, new PlayerId(state1.AdminPlayerId ?? default), InvocationOrigin.System));

        // Now on the last board — place one card (partial placement)
        var state2 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state2.Phase);
        Assert.NotNull(state2.GuessingState);
        Assert.NotNull(state2.PhaseEndsAtUtc);

        var currentOwner = state2.GuessingState!.CurrentBoardOwnerId!.Value;
        var placerId = state2.Players.Select(p => p.PlayerId).First(p => p != currentOwner);
        await placeGuessing.Handle(new PlaceGuessingCard.Request(gameId, new PlayerId(placerId), 0, BoardPosition.TopLeft));

        // Expire the last board (incomplete) — 1er timeout → cooldown de débrief.
        state2 = await getState.Handle(new GetGameState.Request(gameId));
        clock.Set(state2.PhaseEndsAtUtc!.Value.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, new PlayerId(placerId), InvocationOrigin.System));

        // Cooldown actif sur le dernier board.
        var cooldown2 = await repo.Get(gameId) ?? throw new Exception();
        Assert.True(cooldown2.GuessingBoardRevealed);
        Assert.Equal(GamePhase.Guessing, cooldown2.Phase);

        // Fin du cooldown → Scoring.
        clock.Set(cooldown2.PhaseEndsAtUtc!.Value.AddSeconds(1));
        var response = await moveNext.Handle(new MoveToNextBoard.Request(gameId, new PlayerId(placerId), InvocationOrigin.System));

        Assert.Equal(GamePhase.Scoring, response.Phase);
        var state3 = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Scoring, state3.Phase);
        Assert.NotNull(state3.PhaseEndsAtUtc);
    }
}
