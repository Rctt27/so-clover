using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.UseCases.Games;

public interface ICreateGameUseCase : IUseCase<CreateGame.Request, CreateGame.Response> { }

public static class CreateGame
{
    public readonly record struct Request;
    public readonly record struct Response(GameId GameId);

    public sealed class Handler : ICreateGameUseCase
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
            var game = new Game(GameId.New());
            await _repo.Save(game, ct);
            await _events.Publish(new GameCreated(game.Id), ct);
            return new Response(game.Id);
        }
    }
}

public readonly record struct GameCreated(GameId GameId);
