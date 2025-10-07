using System.Collections.Concurrent;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<GameId, Game> _store = new();

    public Task<Game?> Get(GameId id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var game);
        return Task.FromResult<Game?>(game);
    }

    public Task Save(Game game, CancellationToken ct = default)
    {
        _store[game.Id] = game;
        return Task.CompletedTask;
    }

    public Task Delete(GameId id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
