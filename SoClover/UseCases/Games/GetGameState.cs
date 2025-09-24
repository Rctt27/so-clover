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
        IReadOnlyList<PlayerState> Players
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
        string? ExpectedWord // populated only if IncludeSecrets = true
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

            return new Response(game.Id, game.Phase, players);
        }

        private static DirectionState BuildDirectionState(Player player, Direction direction, bool includeSecrets)
        {
            var board = player.Board;
            var hasCard = direction switch
            {
                Direction.Top => board.TopLeft is not null,
                Direction.Right => board.TopRight is not null,
                Direction.Bottom => board.BottomRight is not null,
                Direction.Left => board.BottomLeft is not null,
                _ => false
            };

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
            if (includeSecrets && hasCard)
            {
                // Safe: only call when card exists
                expected = board.GetClueText(direction);
            }

            return new DirectionState(direction, hasCard, isGuessed, label, expected);
        }
    }
}
