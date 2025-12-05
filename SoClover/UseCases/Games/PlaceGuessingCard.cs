using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IPlaceGuessingCardUseCase : IUseCase<PlaceGuessingCard.Request, PlaceGuessingCard.Response> { }

public static class PlaceGuessingCard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, int OutsideCardIndex, BoardPosition Position);
    public readonly record struct Response;

    public sealed class Handler : IPlaceGuessingCardUseCase
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
                throw new InvalidOperationInPhaseException("Can only place cards during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            game.PlaceCardOnGuessingBoard(request.OutsideCardIndex, request.Position);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingCardPlaced(game.Id, request.PlayerId, request.OutsideCardIndex, request.Position), ct);

            return new Response();
        }
    }
}

public readonly record struct GuessingCardPlaced(GameId GameId, PlayerId PlayerId, int OutsideCardIndex, BoardPosition Position);
