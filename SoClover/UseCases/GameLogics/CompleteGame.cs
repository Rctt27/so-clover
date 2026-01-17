using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface ICompleteGameUseCase : IUseCase<CompleteGame.Request, CompleteGame.Response> { }

public static class CompleteGame
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);

    public sealed record Response(GamePhase Phase);

    public sealed class Handler : ICompleteGameUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IDeleteGameUseCase _deleteGame;

        public Handler(IGameRepository repo, IEventPublisher events, IDeleteGameUseCase deleteGame)
        {
            _repo = repo;
            _events = events;
            _deleteGame = deleteGame;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            game.CompleteGame(request.PlayerId);

            // Trigger game deletion instead of just changing phase
            await _deleteGame.Handle(new DeleteGame.Request(game.Id), ct);

            return new Response(game.Phase);
        }
    }
}

public readonly record struct GameCompleted(GameId GameId, PlayerId PlayerId);
