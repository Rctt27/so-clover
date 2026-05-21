using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class ClueExplanationVisibilityTests
{
    private static string DictionariesPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        services.AddSingleton<IWordDictionary>(_ => new FileWordDictionary(DictionariesPath));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();

        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
        services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();

        return services.BuildServiceProvider();
    }

    private static async Task<(GameId GameId, PlayerId AdminId, PlayerId OtherId)> SetupGameUntilGuessing(ServiceProvider sp)
    {
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        var adminId = created.CreatorPlayerId;
        var otherId = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        // Set 4 clues for each player so labels appear in DirectionState.
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, "CL Admin Top"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Right, "CL Admin Right"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Bottom, "CL Admin Bottom"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Left, "CL Admin Left"));
        await setClue.Handle(new SetClue.Request(gameId, otherId, Direction.Top, "CL Bob Top"));
        await setClue.Handle(new SetClue.Request(gameId, otherId, Direction.Right, "CL Bob Right"));
        await setClue.Handle(new SetClue.Request(gameId, otherId, Direction.Bottom, "CL Bob Bottom"));
        await setClue.Handle(new SetClue.Request(gameId, otherId, Direction.Left, "CL Bob Left"));

        // Submit both boards so the guessing phase can start.
        var preGuessing = await repo.Get(gameId) ?? throw new Exception("Game vanished");
        foreach (var pl in preGuessing.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);

        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, true));
        return (gameId, adminId, otherId);
    }

    private static async Task ExhaustAttempts(ServiceProvider sp, GameId gameId, PlayerId guesserId)
    {
        var placeCard = sp.GetRequiredService<IPlaceGuessingCardUseCase>();
        var validate = sp.GetRequiredService<IValidateGuessingBoardUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        // Three validations with wrong placements will drain RemainingAttempts from 3 → 0.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var gameInstance = await repo.Get(gameId) ?? throw new Exception("Game vanished");
            for (int pos = 0; pos < 4; pos++)
            {
                if (gameInstance.GuessedCardPositions[(BoardPosition)pos] == null)
                {
                    int poolIdx = gameInstance.OutsideCards.FindIndex(c => c != null);
                    if (poolIdx != -1)
                        await placeCard.Handle(new PlaceGuessingCard.Request(gameId, guesserId, poolIdx, (BoardPosition)pos));
                }
            }
            await validate.Handle(new ValidateGuessingBoard.Request(gameId, guesserId));
        }
    }

    private static string? TopExplanation(GetGameState.Response state)
        => state.GuessingState?.CurrentBoardClues.FirstOrDefault(c => c.Direction == Direction.Top).Explanation;

    [Fact]
    public async Task ClueExplanation_is_absent_during_WritingClues_even_when_store_has_entry()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var store = sp.GetRequiredService<IAiClueExplanationStore>();

        var created = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = created.GameId;
        var adminId = created.CreatorPlayerId;
        await join.Handle(new JoinGame.Request(gameId, "Bob"));
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, "CL Admin Top"));

        store.Save(gameId, adminId, Direction.Top, "Should not leak during writing phase");

        var state = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.WritingClues, state.Phase);
        // In WritingClues, GuessingState is null entirely — no clue stream exposed.
        Assert.Null(state.GuessingState);
    }

    [Fact]
    public async Task ClueExplanation_is_null_during_Guessing_while_attempts_remain()
    {
        var sp = BuildProvider();
        var store = sp.GetRequiredService<IAiClueExplanationStore>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();

        var (gameId, adminId, otherId) = await SetupGameUntilGuessing(sp);
        store.Save(gameId, adminId, Direction.Top, "Secret explanation — must stay hidden");
        store.Save(gameId, otherId, Direction.Top, "Other secret");

        var state = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(GamePhase.Guessing, state.Phase);
        Assert.True(state.GuessingState!.RemainingAttempts > 0);

        // Board has remaining attempts → no explanation is leaked, regardless of which board is current.
        Assert.Null(TopExplanation(state));
    }

    [Fact]
    public async Task ClueExplanation_is_visible_for_current_board_once_attempts_exhausted()
    {
        var sp = BuildProvider();
        var store = sp.GetRequiredService<IAiClueExplanationStore>();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();

        var (gameId, adminId, otherId) = await SetupGameUntilGuessing(sp);
        store.Save(gameId, adminId, Direction.Top, "Admin top reasoning");
        store.Save(gameId, otherId, Direction.Top, "Other top reasoning");

        var preState = await getState.Handle(new GetGameState.Request(gameId));
        var currentOwnerId = new PlayerId(preState.GuessingState!.CurrentBoardOwnerId!.Value);
        var guesserId = preState.Players.First(p => p.PlayerId != currentOwnerId.Value).PlayerId;

        await ExhaustAttempts(sp, gameId, new PlayerId(guesserId));

        var postState = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(0, postState.GuessingState!.RemainingAttempts);

        // Current board owner: explanation revealed.
        var expected = currentOwnerId == adminId ? "Admin top reasoning" : "Other top reasoning";
        Assert.Equal(expected, TopExplanation(postState));
    }

    [Fact]
    public async Task ClueExplanation_is_null_when_store_has_no_entry_even_after_resolution()
    {
        var sp = BuildProvider();
        var getState = sp.GetRequiredService<IGetGameStateUseCase>();

        var (gameId, _, _) = await SetupGameUntilGuessing(sp);

        var preState = await getState.Handle(new GetGameState.Request(gameId));
        var currentOwnerId = new PlayerId(preState.GuessingState!.CurrentBoardOwnerId!.Value);
        var guesserId = preState.Players.First(p => p.PlayerId != currentOwnerId.Value).PlayerId;

        await ExhaustAttempts(sp, gameId, new PlayerId(guesserId));

        var postState = await getState.Handle(new GetGameState.Request(gameId));
        Assert.Equal(0, postState.GuessingState!.RemainingAttempts);

        // No entry saved in store → human-style clue → no tooltip anywhere.
        Assert.Null(TopExplanation(postState));
    }
}
