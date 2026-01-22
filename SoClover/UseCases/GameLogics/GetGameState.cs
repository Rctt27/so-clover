using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IGetGameStateUseCase : IUseCase<GetGameState.Request, GetGameState.Response> { }

public static class GetGameState
{
    public readonly record struct Request(GameId GameId, bool IncludeSecrets = false, PlayerId? RequestingPlayerId = null);

    public sealed record Response(
        [property: JsonPropertyName("gameId")] Guid GameId,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("cluesDurationSecondsOverride")] int? CluesDurationSecondsOverride,
        [property: JsonPropertyName("guessDurationSecondsOverride")] int? GuessDurationSecondsOverride,
        [property: JsonPropertyName("phase")] GamePhase Phase,
        [property: JsonPropertyName("adminPlayerId")] Guid? AdminPlayerId,
        [property: JsonPropertyName("phaseEndsAtUtc")] DateTime? PhaseEndsAtUtc,
        [property: JsonPropertyName("players")] IReadOnlyList<PlayerState> Players,
        [property: JsonPropertyName("guessingState")] GuessingPhaseState? GuessingState
    );

    public sealed record PlayerState(
        [property: JsonPropertyName("playerId")] Guid PlayerId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("cursorColorIndex")] int CursorColorIndex,
        [property: JsonPropertyName("board")] BoardState Board
    );

    public sealed record BoardState(
        [property: JsonPropertyName("top")] DirectionState Top,
        [property: JsonPropertyName("right")] DirectionState Right,
        [property: JsonPropertyName("bottom")] DirectionState Bottom,
        [property: JsonPropertyName("left")] DirectionState Left,
        [property: JsonPropertyName("isSubmitted")] bool IsSubmitted
    );

    public sealed record DirectionState(
        [property: JsonPropertyName("direction")] Direction Direction,
        [property: JsonPropertyName("hasCard")] bool HasCard,
        [property: JsonPropertyName("isGuessed")] bool IsGuessed,
        [property: JsonPropertyName("clueLabel")] string? ClueLabel,
        [property: JsonPropertyName("expectedWord")] string? ExpectedWord, // populated only if IncludeSecrets = true
        [property: JsonPropertyName("card")] CardInfo? Card // populated only if IncludeSecrets = true
    );

    public sealed record CardInfo(
        [property: JsonPropertyName("cardId")] string CardId,
        [property: JsonPropertyName("topWord")] string TopWord,
        [property: JsonPropertyName("rightWord")] string RightWord,
        [property: JsonPropertyName("bottomWord")] string BottomWord,
        [property: JsonPropertyName("leftWord")] string LeftWord,
        [property: JsonPropertyName("rotation")] string Rotation
    );

    public sealed record GuessingPhaseState(
        [property: JsonPropertyName("currentBoardOwnerId")] Guid? CurrentBoardOwnerId,
        [property: JsonPropertyName("currentBoardOwnerName")] string? CurrentBoardOwnerName,
        [property: JsonPropertyName("outsideCards")] IReadOnlyList<CardInfo?> OutsideCards,
        [property: JsonPropertyName("guessedPositions")] Dictionary<BoardPosition, CardInfo?> GuessedPositions,
        [property: JsonPropertyName("correctlyPlacedPositions")] IReadOnlyList<BoardPosition> CorrectlyPlacedPositions,
        [property: JsonPropertyName("remainingAttempts")] int RemainingAttempts,
        [property: JsonPropertyName("currentBoardClues")] IReadOnlyList<ClueInfo> CurrentBoardClues,
        [property: JsonPropertyName("cumulativeBoardRotation")] int CumulativeBoardRotation
    );

    public sealed record ClueInfo(
        [property: JsonPropertyName("direction")] Direction Direction,
        [property: JsonPropertyName("text")] string Text
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
                .Select(p =>
                {
                    var includeSecretsForPlayer = request.IncludeSecrets
                        || (request.RequestingPlayerId.HasValue && p.Id == request.RequestingPlayerId.Value);

                    return new PlayerState(
                    p.Id.Value,
                    p.Name,
                    p.CursorColorIndex,
                    new BoardState(
                        BuildDirectionState(p, Direction.Top, includeSecretsForPlayer, game.Phase),
                        BuildDirectionState(p, Direction.Right, includeSecretsForPlayer, game.Phase),
                        BuildDirectionState(p, Direction.Bottom, includeSecretsForPlayer, game.Phase),
                        BuildDirectionState(p, Direction.Left, includeSecretsForPlayer, game.Phase),
                        p.Board.IsSubmitted
                    )
                );
                })
                .ToList();

            GuessingPhaseState? guessingState = null;
            if (game.Phase == GamePhase.Guessing && game.CurrentGuessingBoardOwner != null)
            {
                var owner = game.Players.FirstOrDefault(p => p.Id == game.CurrentGuessingBoardOwner);
                var ownerName = owner?.Name;

                var outsideCards = game.OutsideCards.Select(oc => oc == null ? null : new CardInfo(
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
                    game.CurrentGuessingBoardOwner?.Value,
                    ownerName,
                    outsideCards,
                    guessedPositions,
                    game.CorrectlyPlacedPositions.ToList(),
                    game.RemainingAttempts,
                    clues,
                    game.CumulativeBoardRotation
                );
            }

            return new Response(
                game.Id.Value,
                game.Language,
                game.CluesDurationSecondsOverride,
                game.GuessDurationSecondsOverride,
                game.Phase,
                game.AdminPlayerId?.Value,
                game.PhaseEndsAtUtc,
                players,
                guessingState);
        }

        private static DirectionState BuildDirectionState(Player player, Direction direction, bool includeSecrets, GamePhase phase)
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

            string? label = null;
            if (includeSecrets || phase == GamePhase.Guessing || phase == GamePhase.Scoring)
            {
                label = direction switch
                {
                    Direction.Top => board.TopClue?.Value,
                    Direction.Right => board.RightClue?.Value,
                    Direction.Bottom => board.BottomClue?.Value,
                    Direction.Left => board.LeftClue?.Value,
                    _ => null
                };
            }

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
