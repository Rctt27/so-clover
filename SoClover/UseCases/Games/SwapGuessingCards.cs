using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface ISwapGuessingCardsUseCase : IUseCase<SwapGuessingCards.Request, SwapGuessingCards.Response> { }

public static class SwapGuessingCards
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, BoardPosition Position1, BoardPosition Position2);
    public readonly record struct Response;

    public sealed class Handler : ISwapGuessingCardsUseCase
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

            game.SwapGuessingCards(request.Position1, request.Position2);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingCardsSwapped(game.Id, request.PlayerId, request.Position1, request.Position2), ct);

            return new Response();
        }
    }
}

public readonly record struct GuessingCardsSwapped(GameId GameId, PlayerId PlayerId, BoardPosition Position1, BoardPosition Position2);
