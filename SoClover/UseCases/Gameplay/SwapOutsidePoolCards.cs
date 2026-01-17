using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface ISwapOutsidePoolCardsUseCase : IUseCase<SwapOutsidePoolCards.Request, SwapOutsidePoolCards.Response> { }

public static class SwapOutsidePoolCards
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, int Index1, int Index2);
    public readonly record struct Response;

    public sealed class Handler : ISwapOutsidePoolCardsUseCase
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
                throw new InvalidOperationInPhaseException("Can only swap pool cards during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            game.SwapOutsidePoolCards(request.Index1, request.Index2);
            await _repo.Save(game, ct);
            await _events.Publish(new OutsidePoolCardsSwapped(game.Id, request.PlayerId, request.Index1, request.Index2), ct);

            return new Response();
        }
    }
}

public readonly record struct OutsidePoolCardsSwapped(GameId GameId, PlayerId PlayerId, int Index1, int Index2);
