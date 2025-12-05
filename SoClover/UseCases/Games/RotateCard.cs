using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IRotateCardUseCase : IUseCase<RotateCard.Request, RotateCard.Response> { }

public static class RotateCard
{
    public readonly record struct Request(
        GameId GameId,
        PlayerId PlayerId,
        int? OutsideCardIndex = null,
        BoardPosition? BoardPosition = null,
        bool RotateRight = true);

    public readonly record struct Response;

    public sealed class Handler : IRotateCardUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IClock _clock;

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Phase and time guards
            if (game.Phase != GamePhase.Guessing)
                throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            // Déterminer si on rotate une carte outside ou sur le board
            if (request.OutsideCardIndex.HasValue)
            {
                game.RotateCard(request.OutsideCardIndex.Value, request.RotateRight);
                await _repo.Save(game, ct);
                await _events.Publish(new CardRotated(
                    game.Id,
                    request.PlayerId,
                    request.OutsideCardIndex.Value,
                    null,
                    request.RotateRight), ct);
            }
            else if (request.BoardPosition.HasValue)
            {
                game.RotateCard(request.BoardPosition.Value, request.RotateRight);
                await _repo.Save(game, ct);
                await _events.Publish(new CardRotated(
                    game.Id,
                    request.PlayerId,
                    null,
                    request.BoardPosition.Value,
                    request.RotateRight), ct);
            }
            else
            {
                throw new ArgumentException("Either OutsideCardIndex or BoardPosition must be provided.");
            }

            return new Response();
        }
    }
}

public readonly record struct CardRotated(
    GameId GameId,
    PlayerId PlayerId,
    int? OutsideCardIndex,
    BoardPosition? BoardPosition,
    bool RotateRight);
