using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface IReturnGuessingCardUseCase : IUseCase<ReturnGuessingCard.Request, ReturnGuessingCard.Response> { }

public static class ReturnGuessingCard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, BoardPosition Position);
    public readonly record struct Response;

    public sealed class Handler : IReturnGuessingCardUseCase
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
                throw new InvalidOperationInPhaseException("Can only return cards during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            game.ReturnGuessingCard(request.Position);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingCardReturned(game.Id, request.PlayerId, request.Position), ct);

            return new Response();
        }
    }
}

public readonly record struct GuessingCardReturned(GameId GameId, PlayerId PlayerId, BoardPosition Position);
