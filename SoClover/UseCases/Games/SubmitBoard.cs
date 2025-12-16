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
        private readonly IStartGuessingPhaseUseCase _startGuessingPhase;

        public Handler(IGameRepository repo, IEventPublisher events, IStartGuessingPhaseUseCase startGuessingPhase)
        {
            _repo = repo;
            _events = events;
            _startGuessingPhase = startGuessingPhase;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Validate that we're in the WritingClues phase
            if (game.Phase != GamePhase.WritingClues)
                throw new InvalidOperationInPhaseException("Cannot submit board outside WritingClues phase.");

            // Mark this player's board as submitted
            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId) 
                ?? throw new PlayerNotFoundException(request.PlayerId);

            // Optional: prevent submitting an incomplete board
            var boardHasAllClues = player.Board.TopClue != null
                                   && player.Board.RightClue != null
                                   && player.Board.BottomClue != null
                                   && player.Board.LeftClue != null;
            if (!boardHasAllClues)
                throw new InvalidOperationException("Cannot submit an incomplete board.");

            // Idempotent, irreversible mark
            player.Board.MarkSubmitted(DateTime.UtcNow);

            await _repo.Save(game, ct);
            await _events.Publish(new BoardSubmitted(game.Id, request.PlayerId), ct);

            // If all players have submitted, automatically start the guessing phase
            var allPlayersExplicitlySubmitted = game.Players.All(p => p.Board.IsSubmitted);
            if (allPlayersExplicitlySubmitted)
            {
                await _startGuessingPhase.Handle(new StartGuessingPhase.Request(game.Id), ct);
            }

            return new Response();
        }
    }
}

public readonly record struct BoardSubmitted(GameId GameId, PlayerId PlayerId);
