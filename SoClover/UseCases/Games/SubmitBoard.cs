using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface ISubmitBoardUseCase : IUseCase<SubmitBoard.Request, SubmitBoard.Response> { }

public static class SubmitBoard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response;

    public sealed class Handler : ISubmitBoardUseCase
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

            // Validate that we're in the WritingClues phase
            if (game.Phase != GamePhase.WritingClues)
                throw new InvalidOperationInPhaseException("Cannot submit board outside WritingClues phase.");

            // The board submission is recorded in the event system
            // In the future, we could add validation here to ensure all 4 clues are set

            await _repo.Save(game, ct);
            await _events.Publish(new BoardSubmitted(game.Id, request.PlayerId), ct);
            return new Response();
        }
    }
}

public readonly record struct BoardSubmitted(GameId GameId, PlayerId PlayerId);
