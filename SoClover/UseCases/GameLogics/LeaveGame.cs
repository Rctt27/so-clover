using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface ILeaveGameUseCase : IUseCase<LeaveGame.Request, LeaveGame.Response> { }

public static class LeaveGame
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(bool Success);

    public sealed class Handler : ILeaveGameUseCase
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
            
            game.RemovePlayer(request.PlayerId);
            
            await _repo.Save(game, ct);
            await _events.Publish(new PlayerLeft(game.Id, request.PlayerId), ct);
            
            return new Response(true);
        }
    }
}

public readonly record struct PlayerLeft(GameId GameId, PlayerId PlayerId);
