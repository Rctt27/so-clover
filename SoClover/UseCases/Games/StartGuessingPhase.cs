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
        private readonly CardFactory _cardFactory;
        private readonly Random _random = new();

        public Handler(IGameRepository repo, IEventPublisher events, CardFactory cardFactory)
        {
            _repo = repo;
            _events = events;
            _cardFactory = cardFactory;
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

            // Générer la 5ème carte aléatoire
            var fifthCard = await _cardFactory.CreateRandomCardAsync(
                CardId.Create(),
                game.Language,
                ct
            );

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
