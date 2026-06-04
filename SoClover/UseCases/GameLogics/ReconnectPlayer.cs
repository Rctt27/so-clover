using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IReconnectPlayerUseCase : IUseCase<ReconnectPlayer.Request, ReconnectPlayer.Response> { }

public static class ReconnectPlayer
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(bool Reactivated);

    public sealed class Handler : IReconnectPlayerUseCase
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

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player is null || !player.IsDisconnected)
            {
                return new Response(false);
            }

            var reactivated = game.ReconnectPlayer(request.PlayerId);
            if (!reactivated)
            {
                return new Response(false);
            }

            await _repo.Save(game, ct);
            await _events.Publish(new PlayerReconnected(game.Id, request.PlayerId, player.Name), ct);
            return new Response(true);
        }
    }
}

public readonly record struct PlayerReconnected(GameId GameId, PlayerId PlayerId, string PlayerName);
