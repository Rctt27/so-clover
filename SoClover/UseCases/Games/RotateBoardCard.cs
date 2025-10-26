using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IRotateBoardCardUseCase : IUseCase<RotateBoardCard.Request, RotateBoardCard.Response> { }

public static class RotateBoardCard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, BoardPosition Position);
    public readonly record struct Response;

    public sealed class Handler : IRotateBoardCardUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;

        public Handler(IGameRepository repo, IEventPublisher events)
        {
            _repo = repo;
            _events = events;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            game.RotateGuessingCard(request.Position);
            await _repo.Save(game, ct);
            await _events.Publish(new BoardCardRotated(game.Id, request.PlayerId, request.Position), ct);

            return new Response();
        }
    }
}

public readonly record struct BoardCardRotated(GameId GameId, PlayerId PlayerId, BoardPosition Position);
