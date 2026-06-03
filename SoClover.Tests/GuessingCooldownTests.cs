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
    public async Task System_timeout_on_incomplete_board_starts_cooldown_then_second_timeout_advances()
    {
        var clock = new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (gameId, repo, sp) = await BuildGuessingGame(clock);
        using var _ = sp;
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var game = await repo.Get(gameId) ?? throw new Exception();
        var firstOwner = game.CurrentGuessingBoardOwner;
        var guessEnds = game.PhaseEndsAtUtc!.Value;

        // 1er timeout système sur board incomplet → cooldown, PAS d'avance.
        clock.Set(guessEnds.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, firstOwner ?? default, InvocationOrigin.System));

        game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.Equal(firstOwner, game.CurrentGuessingBoardOwner);     // owner inchangé
        Assert.True(game.GuessingBoardRevealed);                      // cooldown actif
        Assert.Equal(0, game.CompletedBoardsCount);                   // pas avancé
        Assert.Equal(clock.UtcNow.AddSeconds(60), game.PhaseEndsAtUtc); // deadline cooldown

        // 2e timeout (fin de cooldown) → avance au board suivant.
        var cooldownEnds = game.PhaseEndsAtUtc!.Value;
        clock.Set(cooldownEnds.AddSeconds(1));
        await moveNext.Handle(new MoveToNextBoard.Request(gameId, firstOwner ?? default, InvocationOrigin.System));

        game = await repo.Get(gameId) ?? throw new Exception();
        Assert.NotEqual(firstOwner, game.CurrentGuessingBoardOwner);
        Assert.False(game.GuessingBoardRevealed);                     // reset sur nouveau board
        Assert.Equal(1, game.CompletedBoardsCount);
    }

    private static readonly BoardPosition[] AllPositions =
        { BoardPosition.TopLeft, BoardPosition.TopRight, BoardPosition.BottomRight, BoardPosition.BottomLeft };

    private static async Task ExhaustAttemptsWithDecoy(
        GameId gameId, IGameRepository repo, IPlaceGuessingCardUseCase place,
        IValidateGuessingBoardUseCase validate, PlayerId guesser)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var game = await repo.Get(gameId) ?? throw new Exception();
            if (game.RemainingAttempts == 0) break;

            var owner = game.Players.First(p => p.Id == game.CurrentGuessingBoardOwner);
            var solutionIds = new HashSet<Guid>
            {
                owner.Board.TopLeft!.Card.Id.Value, owner.Board.TopRight!.Card.Id.Value,
                owner.Board.BottomRight!.Card.Id.Value, owner.Board.BottomLeft!.Card.Id.Value
            };

            // Cases à remplir (non verrouillées + vides).
            var emptyPositions = AllPositions
                .Where(p => !game.CorrectlyPlacedPositions.Contains(p) && game.GuessedCardPositions[p] == null)
                .ToList();

            // Index pool de la carte leurre (présente, hors solution) → garantit ≥1 case fausse.
            int decoyIdx = game.OutsideCards.FindIndex(c => c != null && !solutionIds.Contains(c.Card.Id.Value));

            foreach (var pos in emptyPositions)
            {
                var current = await repo.Get(gameId) ?? throw new Exception();
                int idx = pos == emptyPositions[0] && decoyIdx >= 0
                    ? decoyIdx
                    : current.OutsideCards.FindIndex(c => c != null);
                if (idx < 0) continue;
                await place.Handle(new PlaceGuessingCard.Request(gameId, guesser, idx, pos));
            }

            await validate.Handle(new ValidateGuessingBoard.Request(gameId, guesser));
        }
    }

    [Fact]
    public async Task Three_failed_attempts_start_cooldown()
    {
        var clock = new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var (gameId, repo, sp) = await BuildGuessingGame(clock);
        using var _ = sp;
        var place = sp.GetRequiredService<IPlaceGuessingCardUseCase>();
        var validate = sp.GetRequiredService<IValidateGuessingBoardUseCase>();

        var game = await repo.Get(gameId) ?? throw new Exception();
        var guesser = game.Players.First(p => p.Id != game.CurrentGuessingBoardOwner).Id;

        await ExhaustAttemptsWithDecoy(gameId, repo, place, validate, guesser);

        game = await repo.Get(gameId) ?? throw new Exception();
        Assert.Equal(0, game.RemainingAttempts);
        Assert.True(game.GuessingBoardRevealed);
        Assert.Equal(clock.UtcNow.AddSeconds(60), game.PhaseEndsAtUtc);
        Assert.Equal(GamePhase.Guessing, game.Phase); // pas encore avancé
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
