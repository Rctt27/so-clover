using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public static class StartGuessingPhase
{
    public readonly record struct Request(GameId GameId);
    public readonly record struct Response(GamePhase Phase);

    public sealed class Handler : IUseCase<Request, Response>
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
            game.StartGuessingPhase();
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingPhaseStarted(game.Id), ct);
            return new Response(game.Phase);
        }
    }
}

public readonly record struct GuessingPhaseStarted(GameId GameId);
