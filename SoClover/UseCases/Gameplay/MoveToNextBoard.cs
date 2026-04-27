using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface IMoveToNextBoardUseCase : IUseCase<MoveToNextBoard.Request, MoveToNextBoard.Response> { }

public static class MoveToNextBoard
{
    public readonly record struct Request
    {
        public Request(GameId gameId, PlayerId playerId)
        {
            GameId = gameId;
            PlayerId = playerId;
            Origin = SoClover.UseCases.Abstractions.InvocationOrigin.Client;
        }

        public Request(GameId gameId, PlayerId playerId, SoClover.UseCases.Abstractions.InvocationOrigin origin)
        {
            GameId = gameId;
            PlayerId = playerId;
            Origin = origin;
        }

        public GameId GameId { get; }
        public PlayerId PlayerId { get; }
        public SoClover.UseCases.Abstractions.InvocationOrigin Origin { get; }
    }
    public readonly record struct Response(
        GamePhase Phase,
        PlayerId? NextBoardOwnerId
    );

    public sealed class Handler : IMoveToNextBoardUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IClock _clock;
        private readonly IGameSettingsProvider _settings;
        private readonly IWordDictionary _wordDictionary;
        private readonly IWordsPoolCache _poolCache;
        private readonly Random _random = new();

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock, IGameSettingsProvider settings, IWordDictionary wordDictionary, IWordsPoolCache poolCache)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
            _settings = settings;
            _wordDictionary = wordDictionary;
            _poolCache = poolCache;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            if (game.Phase != GamePhase.Guessing)
                throw new InvalidOperationInPhaseException("Can only move to next board during Guessing phase.");

            var now = _clock.UtcNow;
            var cfg = await _settings.GetAsync(ct);
            var seconds = game.GuessDurationSecondsOverride ?? cfg.GuessDurationSeconds;
            seconds = Math.Clamp(seconds, 1, 1800);
            var perBoard = TimeSpan.FromSeconds(seconds);

            // Allow move if board completed/exhausted OR time expired
            var isTimeExpired = game.PhaseEndsAtUtc.HasValue && now >= game.PhaseEndsAtUtc.Value;
            var isSystemInvocation = request.Origin == SoClover.UseCases.Abstractions.InvocationOrigin.System;

            Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard: Game={game.Id.Value}, Player={request.PlayerId.Value}, Origin={request.Origin}, Now={now:O}, EndsAt={game.PhaseEndsAtUtc?.ToString("O") ?? "null"}");

            // Déterminer si ce passage est le dernier (après ce move, on doit entrer en Scoring)
            var boardsToGuessCount = game.BoardsToGuess.Count;
            // On utilise l'état actuel du compteur. Si on est sur le dernier board,
            // CompletedBoardsCount est à boardsToGuessCount - 1.
            var isLastBoard = game.CompletedBoardsCount >= (boardsToGuessCount - 1);

            Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard Calculation: CompletedBoardsCount={game.CompletedBoardsCount}, BoardsToGuessCount={boardsToGuessCount}, isLastBoard={isLastBoard}");

            // On autorise le passage au board suivant (ou au scoring) si le temps est écoulé,
            // si c'est une action système, si le board est complet ou s'il n'y a plus d'essais.
            // On s'assure ainsi que le dernier board force bien le passage en Scoring lors d'un timeout.
            var isTimeout = isTimeExpired || isSystemInvocation;
            var isBoardComplete = game.CorrectlyPlacedPositions.Count >= 4;
            var noAttemptsLeft = game.RemainingAttempts <= 0;

            if (!isTimeout && !isBoardComplete && !noAttemptsLeft)
            {
                Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard BLOCKED: isTimeout={isTimeout}, isBoardComplete={isBoardComplete}, noAttemptsLeft={noAttemptsLeft}, RemainingAttempts={game.RemainingAttempts}");
                throw new InvalidOperationException("Cannot move to next board while attempts remain and board is not complete.");
            }

            // If we are moving due to time expiration and board is incomplete, record a timeout loss for current board
            if (isTimeout && game.CurrentGuessingBoardOwner is not null && !isBoardComplete)
            {
                Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard: Recording timeout loss for owner {game.CurrentGuessingBoardOwner.Value}");
                game.RecordTimeoutLoss(now);
            }

            // On s'assure que si on est en timeout, on force la transition même si des conditions de plateau bloquent normalement
            if (isTimeout)
            {
                Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard: Timeout forcing transition. isLastBoard={isLastBoard}");
            }

            Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard: Before domain call. Current Phase={game.Phase}, CompletedBoardsCount={game.CompletedBoardsCount}, BoardsToGuessCount={boardsToGuessCount}, isLastBoard={isLastBoard}");

            Card fifthCard;
            Rotation[] rotations;

            if (isLastBoard)
            {
                // Éviter toute dépendance au WordsPool lors du dernier passage (pas nécessaire et peut échouer)
                fifthCard = new Card(CardId.New(), "x", "x", "x", "x");
                rotations = new Rotation[5]; // valeurs par défaut non utilisées si on passe en Scoring
            }
            else
            {
                // Restore WordsPool from cache (survives EF deserialization)
                await EnsureWordsPoolAsync(game, ct);
                fifthCard = game.CreateRandomCard();

                // Générer 5 rotations aléatoires
                rotations = new Rotation[5];
                for (int i = 0; i < 5; i++)
                {
                    rotations[i] = (Rotation)_random.Next(4);
                }
            }

            game.MoveToNextGuessingBoard(fifthCard, rotations, now, perBoard);

            Console.WriteLine($"[DEBUG_LOG] MoveToNextBoard: After domain call. New Phase={game.Phase}, CurrentOwner={game.CurrentGuessingBoardOwner?.Value.ToString() ?? "null"}");

            // If we just entered Scoring, set a scoring deadline
            if (game.Phase == GamePhase.Scoring)
            {
                var scoringSeconds = Math.Clamp(cfg.ScoringDurationSeconds, 1, 1800);
                game.SetScoringDeadline(now, TimeSpan.FromSeconds(scoringSeconds));
            }

            await _repo.Save(game, ct);
            await _events.Publish(new MovedToNextBoard(game.Id, game.CurrentGuessingBoardOwner), ct);

            return new Response(game.Phase, game.CurrentGuessingBoardOwner);
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

public readonly record struct MovedToNextBoard(GameId GameId, PlayerId? NextBoardOwnerId);
