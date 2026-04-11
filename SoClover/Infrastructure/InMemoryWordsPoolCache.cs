using System.Collections.Concurrent;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class InMemoryWordsPoolCache : IWordsPoolCache
{
    private readonly ConcurrentDictionary<GameId, WordsPool> _cache = new();

    public WordsPool? Get(GameId gameId) =>
        _cache.TryGetValue(gameId, out var pool) ? pool : null;

    public void Set(GameId gameId, WordsPool pool) =>
        _cache[gameId] = pool;

    public void Remove(GameId gameId) =>
        _cache.TryRemove(gameId, out _);
}
