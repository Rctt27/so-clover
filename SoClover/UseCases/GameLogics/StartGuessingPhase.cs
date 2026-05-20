using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface IStartGuessingPhaseUseCase : IUseCase<StartGuessingPhase.Request, StartGuessingPhase.Response> { }

public static class StartGuessingPhase
{
    public readonly record struct Request(GameId GameId, bool Force = false);
    public readonly record struct Response(GamePhase Phase, PlayerId CurrentBoardOwner);

    public sealed class Handler : IStartGuessingPhaseUseCase
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

            // Vérifier la phase avant toute autre chose pour que l'exception correcte soit levée
            if (game.Phase != GamePhase.WritingClues)
                throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");

            // Garde Epic 03 : il faut au moins un humain non-déconnecté pour deviner.
            // S'applique même quand Force=true (sinon Guessing infinie sans guesser).
            if (game.GuessingParticipants.Count == 0)
                throw new NoHumanGuesserException();

            // Garde Epic 03 : il faut au moins un board submitted (humain ou AI) à faire deviner.
            if (game.BoardsToGuess.Count == 0)
                throw new NotEnoughPlayersException(1, 0);

            // Vérifier que tous les joueurs ont explicitement soumis leur board, sauf si force est activé
            if (!request.Force)
            {
                var allSubmitted = game.WritingParticipants.All(p => p.Board.IsSubmitted);
                if (!allSubmitted)
                    throw new InvalidOperationInPhaseException("Cannot start Guessing: not all boards were explicitly submitted.");
            }

            // Choisir aléatoirement le premier board à deviner (humain ou AI submitted).
            var boards = game.BoardsToGuess.ToList();
            var firstPlayer = boards[_random.Next(boards.Count)];

            // Restore WordsPool from cache (survives EF deserialization)
            await EnsureWordsPoolAsync(game, ct);

            // Générer la 5ème carte aléatoire depuis le WordsPool de la game
            var fifthCard = game.CreateRandomCard();

            // Générer 5 rotations aléatoires (une pour chaque carte)
            var rotations = new Rotation[5];
            for (int i = 0; i < 5; i++)
            {
                rotations[i] = (Rotation)_random.Next(4);
            }

            var now = _clock.UtcNow;
            var cfg = await _settings.GetAsync(ct);
            var seconds = game.GuessDurationSecondsOverride ?? cfg.GuessDurationSeconds;
            seconds = Math.Clamp(seconds, 1, 1800);
            var perBoard = TimeSpan.FromSeconds(seconds);
            game.StartGuessingPhase(firstPlayer.Id, fifthCard, rotations, now, perBoard);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingPhaseStarted(game.Id, firstPlayer.Id), ct);
            return new Response(game.Phase, firstPlayer.Id);
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

public readonly record struct GuessingPhaseStarted(GameId GameId, PlayerId CurrentBoardOwner);
