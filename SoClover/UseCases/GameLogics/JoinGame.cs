using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IJoinGameUseCase : IUseCase<JoinGame.Request, JoinGame.Response> { }

public static class JoinGame
{
    public readonly record struct Request(GameId GameId, string PlayerName, bool ReplaceExisting = false);

    public readonly record struct Response(
        PlayerId PlayerId,
        bool IsConflict = false,
        PlayerId? ExistingPlayerId = null
    );

    public sealed class Handler : IJoinGameUseCase
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

            var existing = game.FindPlayerByName(request.PlayerName);

            if (existing != null && !request.ReplaceExisting)
            {
                return new Response(default, IsConflict: true, ExistingPlayerId: existing.Id);
            }

            if (existing != null && request.ReplaceExisting)
            {
                var newPlayer = new Player(PlayerId.New(), request.PlayerName);
                var reusedId = game.ReplacePlayer(existing.Id, newPlayer);
                await _repo.Save(game, ct);
                await _events.Publish(new PlayerJoined(game.Id, reusedId), ct);
                return new Response(reusedId);
            }

            var player = new Player(PlayerId.New(), request.PlayerName);
            game.AddPlayer(player);
            await _repo.Save(game, ct);
            await _events.Publish(new PlayerJoined(game.Id, player.Id), ct);
            return new Response(player.Id);
        }
    }
}

public readonly record struct PlayerJoined(GameId GameId, PlayerId PlayerId);
