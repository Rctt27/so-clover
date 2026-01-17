using System.Linq;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IPlaceCardToGuessUseCase : IUseCase<PlaceCardToGuess.Request, PlaceCardToGuess.Response> { }

public static class PlaceCardToGuess
{
    public readonly record struct Request(
        GameId GameId,
        PlayerId PlayerId,
        BoardPosition Position,
        string TopWord,
        string RightWord,
        string BottomWord,
        string LeftWord,
        Rotation Rotation = Rotation.None);

    public readonly record struct Response(CardId CardId);

    public sealed class Handler : IPlaceCardToGuessUseCase
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

            var player = game.Players.FirstOrDefault(p => p.Id.Equals(request.PlayerId))
                         ?? throw new PlayerNotFoundException(request.PlayerId);

            var card = new Card(CardId.New(), request.TopWord, request.RightWord, request.BottomWord, request.LeftWord);
            var oriented = new OrientedCard(card, request.Rotation);
            player.Board.Place(request.Position, oriented);

            await _repo.Save(game, ct);
            await _events.Publish(new CardPlaced(game.Id, request.PlayerId, request.Position, card.Id, request.Rotation), ct);
            return new Response(card.Id);
        }
    }
}

public readonly record struct CardPlaced(GameId GameId, PlayerId PlayerId, BoardPosition Position, CardId CardId, Rotation Rotation);
