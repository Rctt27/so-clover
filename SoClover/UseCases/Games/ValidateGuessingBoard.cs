using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IValidateGuessingBoardUseCase : IUseCase<ValidateGuessingBoard.Request, ValidateGuessingBoard.Response> { }

public static class ValidateGuessingBoard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(
        IReadOnlyList<BoardPosition> CorrectPositions,
        IReadOnlyList<BoardPosition> IncorrectPositions,
        int RemainingAttempts,
        bool IsComplete,
        bool ShouldMoveToNext
    );

    public sealed class Handler : IValidateGuessingBoardUseCase
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

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            var result = game.ValidateGuessingBoard();
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingBoardValidated(
                game.Id,
                request.PlayerId,
                result.CorrectPositions,
                result.IncorrectPositions,
                result.RemainingAttempts,
                result.IsComplete
            ), ct);

            return new Response(
                result.CorrectPositions,
                result.IncorrectPositions,
                result.RemainingAttempts,
                result.IsComplete,
                result.ShouldMoveToNext
            );
        }
    }
}

public readonly record struct GuessingBoardValidated(
    GameId GameId,
    PlayerId PlayerId,
    IReadOnlyList<BoardPosition> CorrectPositions,
    IReadOnlyList<BoardPosition> IncorrectPositions,
    int RemainingAttempts,
    bool IsComplete
);
