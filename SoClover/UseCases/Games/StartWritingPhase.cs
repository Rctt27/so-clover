using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IStartWritingPhaseUseCase : IUseCase<StartWritingPhase.Request, StartWritingPhase.Response> { }

public static class StartWritingPhase
{
    public readonly record struct Request(GameId GameId);
    public readonly record struct Response(GamePhase Phase);

    public sealed class Handler : IStartWritingPhaseUseCase
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
            game.StartWritingPhase();
            await _repo.Save(game, ct);
            await _events.Publish(new WritingPhaseStarted(game.Id), ct);
            return new Response(game.Phase);
        }
    }
}

public readonly record struct WritingPhaseStarted(GameId GameId);
