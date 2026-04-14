using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IKickPlayerUseCase : IUseCase<KickPlayer.Request, KickPlayer.Response> { }

public static class KickPlayer
{
    public readonly record struct Request(GameId GameId, PlayerId TargetPlayerId, PlayerId AdminPlayerId);
    public readonly record struct Response(bool Success, string KickedPlayerName);

    public sealed class Handler : IKickPlayerUseCase
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

            if (game.AdminPlayerId != request.AdminPlayerId)
                throw new UnauthorizedAccessException("Only the admin can kick players.");

            if (request.TargetPlayerId == request.AdminPlayerId)
                throw new InvalidOperationException("Admin cannot kick themselves.");

            var target = game.Players.FirstOrDefault(p => p.Id == request.TargetPlayerId)
                ?? throw new PlayerNotFoundException(request.TargetPlayerId);
            var playerName = target.Name;

            game.RemovePlayer(request.TargetPlayerId);

            await _repo.Save(game, ct);
            await _events.Publish(new PlayerKicked(game.Id, request.TargetPlayerId, playerName), ct);

            return new Response(true, playerName);
        }
    }
}

public readonly record struct PlayerKicked(GameId GameId, PlayerId PlayerId, string PlayerName);
