using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface ICompleteGameUseCase : IUseCase<CompleteGame.Request, CompleteGame.Response> { }

public static class CompleteGame
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);

    public sealed record Response(GamePhase Phase);

    public sealed class Handler : ICompleteGameUseCase
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

            game.CompleteGame(request.PlayerId);

            await _repo.Save(game, ct);
            await _events.Publish(new GameCompleted(game.Id, request.PlayerId), ct);

            return new Response(game.Phase);
        }
    }
}

public readonly record struct GameCompleted(GameId GameId, PlayerId PlayerId);
