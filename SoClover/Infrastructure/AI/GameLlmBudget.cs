using System.Collections.Concurrent;
using SoClover.Domain;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Per-game cap on total LLM calls made over the lifetime of the game (in-process).
/// Prevents a misbehaving prompt or retry loop from running away with cost.
/// IMPORTANT: counters are in-process only — restarting the host resets them.
/// This is acceptable for the POC (decision recorded in Epic 04 spec).
/// </summary>
public sealed class GameLlmBudget
{
    private readonly ConcurrentDictionary<GameId, int> _used = new();
    private readonly int _maxCallsPerGame;

    public GameLlmBudget(int maxCallsPerGame)
    {
        if (maxCallsPerGame < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCallsPerGame),
                maxCallsPerGame,
                "MaxCallsPerGame must be >= 1.");
        }
        _maxCallsPerGame = maxCallsPerGame;
    }

    public int MaxCallsPerGame => _maxCallsPerGame;

    /// <summary>
    /// Atomically increments the counter for <paramref name="gameId"/>. Throws
    /// <see cref="LlmBudgetExhaustedException"/> when the cap would be exceeded.
    /// The counter is NOT incremented on failure, so the caller can rely on
    /// "exception thrown ⇒ no consumption".
    /// </summary>
    public void TryConsume(GameId gameId)
    {
        // AddOrUpdate returns the new value; we read what was committed.
        var newValue = _used.AddOrUpdate(
            gameId,
            addValueFactory: _ => 1,
            updateValueFactory: (_, current) =>
            {
                if (current >= _maxCallsPerGame)
                {
                    // Throw inside the factory: ConcurrentDictionary will propagate
                    // and NOT update the stored value — which is exactly what we want.
                    throw new LlmBudgetExhaustedException(gameId, _maxCallsPerGame);
                }
                return current + 1;
            });

        // Defensive: if max == initial state somehow, AddOrUpdate would have stored 1,
        // which is fine for the first call. Nothing extra to do here.
        _ = newValue;
    }

    public int Used(GameId gameId) => _used.TryGetValue(gameId, out var v) ? v : 0;
}