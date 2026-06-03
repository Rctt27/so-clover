using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface IValidateGuessingBoardUseCase : IUseCase<ValidateGuessingBoard.Request, ValidateGuessingBoard.Response> { }

public static class ValidateGuessingBoard
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId);
    public readonly record struct Response(
        [property: JsonPropertyName("correctPositions")] IReadOnlyList<BoardPosition> CorrectPositions,
        [property: JsonPropertyName("incorrectPositions")] IReadOnlyList<BoardPosition> IncorrectPositions,
        [property: JsonPropertyName("remainingAttempts")] int RemainingAttempts,
        [property: JsonPropertyName("isComplete")] bool IsComplete,
        [property: JsonPropertyName("shouldMoveToNext")] bool ShouldMoveToNext
    );

    public sealed class Handler : IValidateGuessingBoardUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IClock _clock;
        private readonly IGameSettingsProvider _settings;

        public Handler(IGameRepository repo, IEventPublisher events, IClock clock, IGameSettingsProvider settings)
        {
            _repo = repo;
            _events = events;
            _clock = clock;
            _settings = settings;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Phase and time guards
            if (game.Phase != GamePhase.Guessing)
                throw new InvalidOperationInPhaseException("Can only validate during Guessing phase.");

            if (game.PhaseEndsAtUtc is DateTime endsAt && _clock.UtcNow >= endsAt)
                throw new InvalidOperationException("Guessing time is over.");

            // Vérifier que le joueur n'est pas le propriétaire du board
            if (game.CurrentGuessingBoardOwner == request.PlayerId)
                throw new InvalidOperationException("Board owner cannot participate in guessing their own board.");

            var result = game.ValidateGuessingBoard();

            // Échec définitif (plus de tentatives, board non complété) → cooldown de débrief.
            if (result.ShouldMoveToNext && !result.IsComplete)
            {
                var cfg = await _settings.GetAsync(ct);
                var cooldownSeconds = Math.Clamp(cfg.GuessingCooldownSeconds > 0 ? cfg.GuessingCooldownSeconds : 60, 1, 1800);
                game.StartGuessingCooldown(_clock.UtcNow, TimeSpan.FromSeconds(cooldownSeconds));
            }

            await _repo.Save(game, ct);
            await _events.Publish(new GuessingBoardValidated(
                game.Id,
                request.PlayerId,
                result.CorrectPositions,
                result.IncorrectPositions,
                result.RemainingAttempts,
                result.IsComplete
            ), ct);

            return new Response(
                result.CorrectPositions,
                result.IncorrectPositions,
                result.RemainingAttempts,
                result.IsComplete,
                result.ShouldMoveToNext
            );
        }
    }
}

public readonly record struct GuessingBoardValidated(
    [property: JsonPropertyName("gameId")] GameId GameId,
    [property: JsonPropertyName("playerId")] PlayerId PlayerId,
    [property: JsonPropertyName("correctPositions")] IReadOnlyList<BoardPosition> CorrectPositions,
    [property: JsonPropertyName("incorrectPositions")] IReadOnlyList<BoardPosition> IncorrectPositions,
    [property: JsonPropertyName("remainingAttempts")] int RemainingAttempts,
    [property: JsonPropertyName("isComplete")] bool IsComplete
);
