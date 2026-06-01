using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GuessingFailedPlacementsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddSingleton<SoClover.Infrastructure.AI.IAiClueExplanationStore, SoClover.Infrastructure.AI.InMemoryAiClueExplanationStore>();
        services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
        return services.BuildServiceProvider();
    }

    private static async Task<(GameId gameId, IGameRepository repo)> ReachGuessing(ServiceProvider sp)
    {
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submitBoard = sp.GetRequiredService<ISubmitBoardUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResp.GameId;
        var adminId = createResp.CreatorPlayerId;
        var p1 = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;
        var p2 = (await join.Handle(new JoinGame.Request(gameId, "Bob"))).PlayerId;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        foreach (var pid in new[] { adminId, p1, p2 })
        {
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Top, "c1"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Right, "c2"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Bottom, "c3"));
            await setClue.Handle(new SetClue.Request(gameId, pid, Direction.Left, "c4"));
            await submitBoard.Handle(new SubmitBoard.Request(gameId, pid));
        }
        Assert.Equal(GamePhase.Guessing, (await repo.Get(gameId))!.Phase);
        return (gameId, repo);
    }

    /// Remplit le board courant avec la carte bonus (5e) en TopLeft (= forcément faux),
    /// puis 3 autres cartes du pool. Retourne l'id (Guid) de la carte bonus.
    private static Guid ForceFailedBoard(Game game)
    {
        var owner = game.Players.First(p => p.Id == game.CurrentGuessingBoardOwner!.Value);
        var boardCardIds = new HashSet<Guid>(new[]
        {
            owner.Board.TopLeft!.Card.Id.Value,
            owner.Board.TopRight!.Card.Id.Value,
            owner.Board.BottomRight!.Card.Id.Value,
            owner.Board.BottomLeft!.Card.Id.Value,
        });

        int bonusIndex = -1;
        for (int i = 0; i < game.OutsideCards.Count; i++)
        {
            var oc = game.OutsideCards[i];
            if (oc != null && !boardCardIds.Contains(oc.Card.Id.Value)) { bonusIndex = i; break; }
        }
        Assert.True(bonusIndex >= 0, "carte bonus introuvable dans le pool");
        var bonusId = game.OutsideCards[bonusIndex]!.Card.Id.Value;

        game.PlaceCardOnGuessingBoard(bonusIndex, BoardPosition.TopLeft);

        int pi = 0;
        foreach (var pos in new[] { BoardPosition.TopRight, BoardPosition.BottomRight, BoardPosition.BottomLeft })
        {
            while (pi < game.OutsideCards.Count && game.OutsideCards[pi] == null) pi++;
            Assert.True(pi < game.OutsideCards.Count, "plus de cartes disponibles dans le pool");
            game.PlaceCardOnGuessingBoard(pi, pos);
            pi++;
        }
        return bonusId;
    }

    [Fact]
    public async Task FailedPlacements_is_empty_when_guessing_starts()
    {
        var sp = BuildProvider();
        var (gameId, repo) = await ReachGuessing(sp);
        Assert.Empty((await repo.Get(gameId))!.FailedPlacements);
    }

    [Fact]
    public async Task Validate_records_incorrect_card_position_rotation()
    {
        var sp = BuildProvider();
        var (gameId, repo) = await ReachGuessing(sp);
        var game = (await repo.Get(gameId))!;

        var bonusId = ForceFailedBoard(game);
        var result = game.ValidateGuessingBoard();

        Assert.Contains(BoardPosition.TopLeft, result.IncorrectPositions);
        Assert.Contains(game.FailedPlacements, f => f.Position == BoardPosition.TopLeft && f.CardId == bonusId);
        // Aucune position jugée correcte n'est enregistrée comme ratée.
        foreach (var f in game.FailedPlacements)
            Assert.DoesNotContain(f.Position, result.CorrectPositions);
    }

    [Fact]
    public async Task GetGameState_exposes_failed_placements()
    {
        var sp = BuildProvider();
        var (gameId, repo) = await ReachGuessing(sp);
        var game = (await repo.Get(gameId))!;
        var bonusId = ForceFailedBoard(game);
        game.ValidateGuessingBoard();
        await repo.Save(game);

        var getState = sp.GetRequiredService<IGetGameStateUseCase>();
        var state = await getState.Handle(new GetGameState.Request(gameId));

        Assert.NotNull(state.GuessingState);
        Assert.Contains(state.GuessingState!.FailedPlacements,
            f => f.Position == BoardPosition.TopLeft && f.CardId == bonusId.ToString());
    }
}
