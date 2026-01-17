using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.UseCases.GameLogics;

public interface IDeleteGameUseCase : IUseCase<DeleteGame.Request, DeleteGame.Response> { }

public static class DeleteGame
{
    public readonly record struct Request(GameId GameId);
    public readonly record struct Response(bool Success);

    public sealed class Handler : IDeleteGameUseCase
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
            var game = await _repo.Get(request.GameId, ct);
            if (game is null)
            {
                return new Response(false);
            }

            await _repo.Delete(request.GameId, ct);
            await _events.Publish(new GameDeleted(request.GameId), ct);
            return new Response(true);
        }
    }
}

public readonly record struct GameDeleted(GameId GameId);
