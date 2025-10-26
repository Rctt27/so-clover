using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IGetGameStateUseCase : IUseCase<GetGameState.Request, GetGameState.Response> { }

public static class GetGameState
{
    public readonly record struct Request(GameId GameId, bool IncludeSecrets = false);

    public sealed record Response(
        GameId GameId,
        GamePhase Phase,
        IReadOnlyList<PlayerState> Players,
        GuessingPhaseState? GuessingState
    );

    public sealed record PlayerState(
        PlayerId PlayerId,
        string Name,
        BoardState Board
    );

    public sealed record BoardState(
        DirectionState Top,
        DirectionState Right,
        DirectionState Bottom,
        DirectionState Left
    );

    public sealed record DirectionState(
        Direction Direction,
        bool HasCard,
        bool IsGuessed,
        string? ClueLabel,
        string? ExpectedWord, // populated only if IncludeSecrets = true
        CardInfo? Card // populated only if IncludeSecrets = true
    );

    public sealed record CardInfo(
        string CardId,
        string TopWord,
        string RightWord,
        string BottomWord,
        string LeftWord,
        string Rotation
    );

    public sealed record GuessingPhaseState(
        PlayerId? CurrentBoardOwnerId,
        string? CurrentBoardOwnerName,
        IReadOnlyList<CardInfo> OutsideCards,
        Dictionary<BoardPosition, CardInfo?> GuessedPositions,
        IReadOnlyList<BoardPosition> CorrectlyPlacedPositions,
        int RemainingAttempts,
        IReadOnlyList<ClueInfo> CurrentBoardClues
    );

    public sealed record ClueInfo(
        Direction Direction,
        string Text
    );

    public sealed class Handler : IGetGameStateUseCase
    {
        private readonly IGameRepository _repo;

        public Handler(IGameRepository repo)
        {
            _repo = repo;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            var players = game.Players
                .Select(p => new PlayerState(
                    p.Id,
                    p.Name,
                    new BoardState(
                        BuildDirectionState(p, Direction.Top, request.IncludeSecrets),
                        BuildDirectionState(p, Direction.Right, request.IncludeSecrets),
                        BuildDirectionState(p, Direction.Bottom, request.IncludeSecrets),
                        BuildDirectionState(p, Direction.Left, request.IncludeSecrets)
                    )
                ))
                .ToList();

            GuessingPhaseState? guessingState = null;
            if (game.Phase == GamePhase.Guessing && game.CurrentGuessingBoardOwner != null)
            {
                var owner = game.Players.FirstOrDefault(p => p.Id == game.CurrentGuessingBoardOwner);
                var ownerName = owner?.Name;

                var outsideCards = game.OutsideCards.Select(oc => new CardInfo(
                    oc.Card.Id.Value.ToString(),
                    oc.Card.TopWord,
                    oc.Card.RightWord,
                    oc.Card.BottomWord,
                    oc.Card.LeftWord,
                    oc.Rotation.ToString()
                )).ToList();

                var guessedPositions = game.GuessedCardPositions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value == null ? null : new CardInfo(
                        kvp.Value.Card.Id.Value.ToString(),
                        kvp.Value.Card.TopWord,
                        kvp.Value.Card.RightWord,
                        kvp.Value.Card.BottomWord,
                        kvp.Value.Card.LeftWord,
                        kvp.Value.Rotation.ToString()
                    )
                );

                var clues = new List<ClueInfo>();
                if (owner != null)
                {
                    if (owner.Board.TopClue != null)
                        clues.Add(new ClueInfo(Direction.Top, owner.Board.TopClue.Value.Value));
                    if (owner.Board.RightClue != null)
                        clues.Add(new ClueInfo(Direction.Right, owner.Board.RightClue.Value.Value));
                    if (owner.Board.BottomClue != null)
                        clues.Add(new ClueInfo(Direction.Bottom, owner.Board.BottomClue.Value.Value));
                    if (owner.Board.LeftClue != null)
                        clues.Add(new ClueInfo(Direction.Left, owner.Board.LeftClue.Value.Value));
                }

                guessingState = new GuessingPhaseState(
                    game.CurrentGuessingBoardOwner,
                    ownerName,
                    outsideCards,
                    guessedPositions,
                    game.CorrectlyPlacedPositions.ToList(),
                    game.RemainingAttempts,
                    clues
                );
            }

            return new Response(game.Id, game.Phase, players, guessingState);
        }

        private static DirectionState BuildDirectionState(Player player, Direction direction, bool includeSecrets)
        {
            var board = player.Board;
            var orientedCard = direction switch
            {
                Direction.Top => board.TopLeft,
                Direction.Right => board.TopRight,
                Direction.Bottom => board.BottomRight,
                Direction.Left => board.BottomLeft,
                _ => null
            };

            var hasCard = orientedCard is not null;
            var isGuessed = board.IsDirectionGuessed(direction);

            string? label = direction switch
            {
                Direction.Top => board.TopClue?.Value,
                Direction.Right => board.RightClue?.Value,
                Direction.Bottom => board.BottomClue?.Value,
                Direction.Left => board.LeftClue?.Value,
                _ => null
            };

            string? expected = null;
            CardInfo? cardInfo = null;

            if (includeSecrets && hasCard && orientedCard is not null)
            {
                // Safe: only call when card exists
                expected = board.GetClueText(direction);

                // Extract full card information
                var card = orientedCard.Card;
                cardInfo = new CardInfo(
                    card.Id.Value.ToString(),
                    card.TopWord,
                    card.RightWord,
                    card.BottomWord,
                    card.LeftWord,
                    orientedCard.Rotation.ToString()
                );
            }

            return new DirectionState(direction, hasCard, isGuessed, label, expected, cardInfo);
        }
    }
}
