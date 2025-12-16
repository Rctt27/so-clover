using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

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
        private readonly Random _random = new();

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

            // Vérifier la phase avant toute autre chose pour que l'exception correcte soit levée
            if (game.Phase != GamePhase.WritingClues)
                throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");

            // Vérifier que tous les joueurs ont explicitement soumis leur board, sauf si force est activé
            if (!request.Force)
            {
                var allSubmitted = game.Players.All(p => p.Board.IsSubmitted);
                if (!allSubmitted)
                    throw new InvalidOperationInPhaseException("Cannot start Guessing: not all boards were explicitly submitted.");
            }

            // Choisir aléatoirement le premier joueur
            var players = game.Players.ToList();
            if (players.Count == 0)
                throw new NotEnoughPlayersException(1, 0);

            var firstPlayer = players[_random.Next(players.Count)];

            // Ensure WordsPool is initialized after loading from persistence
            await game.EnsureWordsPoolInitializedAsync(_wordDictionary, ct);

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
    }
}

public readonly record struct GuessingPhaseStarted(GameId GameId, PlayerId CurrentBoardOwner);
