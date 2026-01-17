using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface ISetClueUseCase : IUseCase<SetClue.Request, SetClue.Response> { }

public static class SetClue
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, Direction Direction, string ClueText);
    public readonly record struct Response;

    public sealed class Handler : ISetClueUseCase
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
            game.SetClue(request.PlayerId, request.Direction, request.ClueText);
            await _repo.Save(game, ct);
            await _events.Publish(new ClueSet(game.Id, request.PlayerId, request.Direction), ct);
            return new Response();
        }
    }
}

public readonly record struct ClueSet(GameId GameId, PlayerId PlayerId, Direction Direction);
