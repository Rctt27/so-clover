using SoClover.Domain;

namespace SoClover.Infrastructure.AI;

public interface IAiClueExplanationStore
{
    void Save(GameId gameId, PlayerId playerId, Direction direction, string explanation);
    string? GetFor(GameId gameId, PlayerId playerId, Direction direction);
    IReadOnlyList<AiClueExplanationEntry> GetAll(GameId gameId);
}

public readonly record struct AiClueExplanationEntry(
    PlayerId PlayerId,
    Direction Direction,
    string Explanation);
