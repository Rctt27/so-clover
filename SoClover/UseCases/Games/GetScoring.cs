using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IGetScoringUseCase : IUseCase<GetScoring.Request, GetScoring.Response> { }

public static class GetScoring
{
    public readonly record struct Request(GameId GameId);

    public sealed record Response(
        IReadOnlyList<BoardResultDto> SuccessfulBoards,
        IReadOnlyList<BoardResultDto> FailedBoards
    );

    public sealed record BoardResultDto(
        string PlayerId,
        string PlayerName,
        int Attempts,
        int DurationSeconds,
        bool WasGuessed
    );

    public sealed class Handler : IGetScoringUseCase
    {
        private readonly IGameRepository _repo;

        public Handler(IGameRepository repo)
        {
            _repo = repo;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            if (game.Phase != GamePhase.Scoring)
                throw new InvalidOperationInPhaseException("Cannot get scoring outside Scoring phase.");

            // Debug: Log player count and board results count
            Console.WriteLine($"[GetScoring] Total players: {game.Players.Count}");
            Console.WriteLine($"[GetScoring] Board results: {game.BoardResults.Count}");
            foreach (var player in game.Players)
            {
                Console.WriteLine($"  - Player: {player.Name} (ID: {player.Id.Value}, IsAdmin: {player.IsAdmin})");
            }

            // Récupérer tous les résultats
            var allResults = game.BoardResults
                .Select(kvp =>
                {
                    var player = game.Players.FirstOrDefault(p => p.Id == kvp.Key);
                    var playerName = player?.Name ?? "Unknown";

                    return new BoardResultDto(
                        kvp.Key.Value.ToString(),
                        playerName,
                        kvp.Value.Attempts,
                        (int)kvp.Value.Duration.TotalSeconds,
                        kvp.Value.WasGuessed
                    );
                })
                .ToList();

            // Séparer les boards réussis et échoués
            var successfulBoards = allResults
                .Where(r => r.WasGuessed)
                .OrderBy(r => r.Attempts)           // D'abord par nombre de tentatives (croissant)
                .ThenBy(r => r.DurationSeconds)     // Puis par durée (croissant)
                .ToList();

            var failedBoards = allResults
                .Where(r => !r.WasGuessed)
                .OrderBy(r => r.Attempts)           // Trier aussi les échecs par tentatives
                .ThenBy(r => r.DurationSeconds)
                .ToList();

            return new Response(successfulBoards, failedBoards);
        }
    }
}
