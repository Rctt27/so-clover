using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface IRotateBoardUseCase : IUseCase<RotateBoard.Request, RotateBoard.Response> { }

public static class RotateBoard
{
    public readonly record struct Request(
        GameId GameId,
        PlayerId PlayerId,
        int CumulativeRotation);

    public readonly record struct Response;

    public sealed class Handler : IRotateBoardUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IClock _clock;

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            if (game.Phase != GamePhase.Guessing)
                throw new InvalidOperationInPhaseException("Can only rotate board during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            game.RotateBoard(request.CumulativeRotation);
            await _repo.Save(game, ct);

            await _events.Publish(new BoardRotated(
                game.Id,
                request.PlayerId,
                request.CumulativeRotation), ct);

            return new Response();
        }
    }
}

public readonly record struct BoardRotated(
    [property: JsonPropertyName("gameId")] GameId GameId,
    [property: JsonPropertyName("playerId")] PlayerId PlayerId,
    [property: JsonPropertyName("cumulativeRotation")] int CumulativeRotation);
