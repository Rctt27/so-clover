using System.Collections.Concurrent;
using SoClover.Domain;

namespace SoClover.Infrastructure.AI;

public sealed class InMemoryAiClueExplanationStore : IAiClueExplanationStore
{
    private readonly ConcurrentDictionary<(GameId, PlayerId, Direction), string> _entries = new();

    public void Save(GameId gameId, PlayerId playerId, Direction direction, string explanation)
    {
        _entries[(gameId, playerId, direction)] = explanation;
    }

    public string? GetFor(GameId gameId, PlayerId playerId, Direction direction)
    {
        return _entries.TryGetValue((gameId, playerId, direction), out var v) ? v : null;
    }

    public IReadOnlyList<AiClueExplanationEntry> GetAll(GameId gameId)
    {
        return _entries
            .Where(kv => kv.Key.Item1 == gameId)
            .Select(kv => new AiClueExplanationEntry(kv.Key.Item2, kv.Key.Item3, kv.Value))
            .ToList()
            .AsReadOnly();
    }
}
