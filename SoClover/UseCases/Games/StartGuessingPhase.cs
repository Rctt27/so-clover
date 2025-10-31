//TODO: Add logic of chrono to this phase. User must not ewceed xx minutes to write Clues. If not all Clues are written when chrono is over then replace "null" Clues by "no clue"

using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IStartGuessingPhaseUseCase : IUseCase<StartGuessingPhase.Request, StartGuessingPhase.Response> { }

public static class StartGuessingPhase
{
    public readonly record struct Request(GameId GameId);
    public readonly record struct Response(GamePhase Phase, PlayerId CurrentBoardOwner);

    public sealed class Handler : IStartGuessingPhaseUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly Random _random = new();

        public Handler(IGameRepository repo, IEventPublisher events)
        {
            _repo = repo;
            _events = events;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Vérifier la phase avant toute autre chose pour que l'exception correcte soit levée
            if (game.Phase != GamePhase.WritingClues)
                throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");

            // Vérifier que tous les joueurs ont soumis leur board
            // (pour l'instant, on suppose que c'est fait via l'événement BoardSubmitted)

            // Choisir aléatoirement le premier joueur
            var players = game.Players.ToList();
            if (players.Count == 0)
                throw new NotEnoughPlayersException(1, 0);

            var firstPlayer = players[_random.Next(players.Count)];

            // Générer la 5ème carte aléatoire depuis le WordsPool de la game
            var fifthCard = game.CreateRandomCard();

            // Générer 5 rotations aléatoires (une pour chaque carte)
            var rotations = new Rotation[5];
            for (int i = 0; i < 5; i++)
            {
                rotations[i] = (Rotation)_random.Next(4);
            }

            game.StartGuessingPhase(firstPlayer.Id, fifthCard, rotations);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessingPhaseStarted(game.Id, firstPlayer.Id), ct);
            return new Response(game.Phase, firstPlayer.Id);
        }
    }
}

public readonly record struct GuessingPhaseStarted(GameId GameId, PlayerId CurrentBoardOwner);
