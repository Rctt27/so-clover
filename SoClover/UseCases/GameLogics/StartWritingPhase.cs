using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SoClover.Domain;
using SoClover.Infrastructure.AI;
using SoClover.RealTime;
using SoClover.UseCases.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

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
        private readonly IWordsPoolCache _poolCache;
        private readonly IConnectionTracker? _connectionTracker;
        private readonly AiClueWorkChannel? _aiClueChannel;
        private readonly ILogger<Handler> _logger;

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock, IGameSettingsProvider settings, IWordDictionary wordDictionary, IWordsPoolCache poolCache, IConnectionTracker? connectionTracker = null, AiClueWorkChannel? aiClueChannel = null, ILogger<Handler>? logger = null)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
            _settings = settings;
            _wordDictionary = wordDictionary;
            _poolCache = poolCache;
            _connectionTracker = connectionTracker;
            _aiClueChannel = aiClueChannel;
            _logger = logger ?? NullLogger<Handler>.Instance;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Verify all human players have an active SignalR connection (AI players are exempt; tracker is null in tests)
            if (_connectionTracker != null)
            {
                // Les joueurs AI n'ont jamais de connexion SignalR — on les exclut du check.
                var disconnectedNames = game.Players
                    .Where(p => !p.IsAI && !_connectionTracker.IsPlayerConnected(p.Id))
                    .Select(p => p.Name)
                    .ToList();
                if (disconnectedNames.Count > 0)
                {
                    throw new DisconnectedPlayersException(disconnectedNames);
                }
            }

            // Restore WordsPool from cache (survives EF deserialization)
            await EnsureWordsPoolAsync(game, ct);

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

            if (_aiClueChannel != null)
            {
                foreach (var aiPlayer in game.Players.Where(p => p.IsAI))
                {
                    if (!_aiClueChannel.Writer.TryWrite(new AiClueGenerationRequested(game.Id, aiPlayer.Id)))
                        _logger.LogWarning(
                            "AI clue channel full — generation request dropped: game={GameId} player={PlayerId}",
                            game.Id.Value, aiPlayer.Id.Value);
                }
            }

            return new Response(game.Phase);
        }

        private async Task EnsureWordsPoolAsync(Game game, CancellationToken ct)
        {
            if (game.IsWordsPoolInitialized) return;

            var cached = _poolCache.Get(game.Id);
            if (cached != null)
            {
                game.AttachWordsPool(cached);
                return;
            }

            var pool = await game.InitializeWordsPoolAsync(_wordDictionary, ct);
            _poolCache.Set(game.Id, pool);
        }
    }
}

public readonly record struct WritingPhaseStarted(GameId GameId);
