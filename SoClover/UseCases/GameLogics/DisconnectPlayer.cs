using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IDisconnectPlayerUseCase : IUseCase<DisconnectPlayer.Request, DisconnectPlayer.Response> { }

public static class DisconnectPlayer
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(bool Success, string PlayerName);

    public sealed class Handler : IDisconnectPlayerUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IStartGuessingPhaseUseCase _startGuessing;

        public Handler(IGameRepository repo, IEventPublisher events, IStartGuessingPhaseUseCase startGuessing)
        {
            _repo = repo;
            _events = events;
            _startGuessing = startGuessing;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId)
                ?? throw new PlayerNotFoundException(request.PlayerId);
            var playerName = player.Name;

            game.DisconnectPlayerDuringWriting(request.PlayerId);
            await _repo.Save(game, ct);
            await _events.Publish(new PlayerDisconnected(game.Id, request.PlayerId, playerName), ct);

            // Check if all remaining active players have submitted
            var allActiveSubmitted = game.ActivePlayers.All(p => p.Board.IsSubmitted);
            if (allActiveSubmitted && game.ActivePlayers.Count > 0)
            {
                await _startGuessing.Handle(new StartGuessingPhase.Request(game.Id), ct);
            }

            return new Response(true, playerName);
        }
    }
}

public readonly record struct PlayerDisconnected(GameId GameId, PlayerId PlayerId, string PlayerName);
