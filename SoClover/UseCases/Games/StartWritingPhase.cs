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
        private readonly IClock _clock;
        private readonly IGameSettingsProvider _settings;
        private readonly IWordDictionary _wordDictionary;

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock, IGameSettingsProvider settings, IWordDictionary wordDictionary)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
            _settings = settings;
            _wordDictionary = wordDictionary;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Ensure the game's WordsPool is available after loading from persistence
            await game.EnsureWordsPoolInitializedAsync(_wordDictionary, ct);

            // Populate each player's board with 4 cards using the game's WordsPool
            foreach (var player in game.Players)
            {
                var cards = new List<Card>(4);
                for (int i = 0; i < 4; i++)
                {
                    var card = game.CreateRandomCard();
                    cards.Add(card);
                }

                player.Board.Place(BoardPosition.TopLeft,     new OrientedCard(cards[0]));
                player.Board.Place(BoardPosition.TopRight,    new OrientedCard(cards[1]));
                player.Board.Place(BoardPosition.BottomRight, new OrientedCard(cards[2]));
                player.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(cards[3]));
            }

            var now = _clock.UtcNow;
            var settings = await _settings.GetAsync(ct);
            var seconds = game.CluesDurationSecondsOverride ?? settings.CluesDurationSeconds;
            seconds = Math.Clamp(seconds, 1, 1800);
            var duration = TimeSpan.FromSeconds(seconds);
            game.StartWritingPhase(now, duration);
            await _repo.Save(game, ct);
            await _events.Publish(new WritingPhaseStarted(game.Id), ct);
            return new Response(game.Phase);
        }
    }
}

public readonly record struct WritingPhaseStarted(GameId GameId);
