using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IMoveToNextBoardUseCase : IUseCase<MoveToNextBoard.Request, MoveToNextBoard.Response> { }

public static class MoveToNextBoard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(
        GamePhase Phase,
        PlayerId? NextBoardOwnerId
    );

    public sealed class Handler : IMoveToNextBoardUseCase
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

            if (game.Phase != GamePhase.Guessing)
                throw new InvalidOperationInPhaseException("Can only move to next board during Guessing phase.");

            // Vérifier que le board actuel est bien validé (soit complété, soit plus de tentatives)
            if (game.RemainingAttempts > 0 && game.CorrectlyPlacedPositions.Count < 4)
                throw new InvalidOperationException("Cannot move to next board while attempts remain and board is not complete.");

            // Générer la 5ème carte aléatoire depuis le WordsPool de la game
            var fifthCard = game.CreateRandomCard();

            // Générer 5 rotations aléatoires
            var rotations = new Rotation[5];
            for (int i = 0; i < 5; i++)
            {
                rotations[i] = (Rotation)_random.Next(4);
            }

            game.MoveToNextGuessingBoard(fifthCard, rotations);

            await _repo.Save(game, ct);
            await _events.Publish(new MovedToNextBoard(game.Id, game.CurrentGuessingBoardOwner), ct);

            return new Response(game.Phase, game.CurrentGuessingBoardOwner);
        }
    }
}

public readonly record struct MovedToNextBoard(GameId GameId, PlayerId? NextBoardOwnerId);
