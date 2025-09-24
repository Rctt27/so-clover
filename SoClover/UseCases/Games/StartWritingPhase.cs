using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IStartWritingPhaseUseCase : IUseCase<StartWritingPhase.Request, StartWritingPhase.Response> { }

public static class StartWritingPhase
{
    public readonly record struct Request(GameId GameId);
    public readonly record struct Response(GamePhase Phase);

    public sealed class Handler : IStartWritingPhaseUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IWordDictionary _dictionary;

        public Handler(IGameRepository repo, IEventPublisher events, IWordDictionary dictionary)
        {
            _repo = repo;
            _events = events;
            _dictionary = dictionary;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Populate each player's board with 4 cards (16 unique words per player)
            foreach (var player in game.Players)
            {
                var words = await _dictionary.TakeWords(game.Id, 16, ct);
                var cards = new List<Card>(4)
                {
                    new Card(CardId.New(), words[0],  words[1],  words[2],  words[3]),
                    new Card(CardId.New(), words[4],  words[5],  words[6],  words[7]),
                    new Card(CardId.New(), words[8],  words[9],  words[10], words[11]),
                    new Card(CardId.New(), words[12], words[13], words[14], words[15])
                };

                player.Board.Place(BoardPosition.TopLeft,     new OrientedCard(cards[0]));
                player.Board.Place(BoardPosition.TopRight,    new OrientedCard(cards[1]));
                player.Board.Place(BoardPosition.BottomRight, new OrientedCard(cards[2]));
                player.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(cards[3]));
            }

            game.StartWritingPhase();
            await _repo.Save(game, ct);
            await _events.Publish(new WritingPhaseStarted(game.Id), ct);
            return new Response(game.Phase);
        }
    }
}

public readonly record struct WritingPhaseStarted(GameId GameId);
