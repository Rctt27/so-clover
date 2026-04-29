using SoClover.Domain;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GameLlmBudgetTests
{
    [Fact]
    public void First_call_for_a_game_is_consumed()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 5);
        var gameId = GameId.New();

        budget.TryConsume(gameId);

        Assert.Equal(1, budget.Used(gameId));
    }

    [Fact]
    public void Consume_up_to_max_succeeds()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 3);
        var gameId = GameId.New();

        budget.TryConsume(gameId);
        budget.TryConsume(gameId);
        budget.TryConsume(gameId);

        Assert.Equal(3, budget.Used(gameId));
    }

    [Fact]
    public void Consuming_beyond_max_throws_LlmBudgetExhaustedException()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 200);
        var gameId = GameId.New();

        for (int i = 0; i < 200; i++)
        {
            budget.TryConsume(gameId);
        }

        var ex = Assert.Throws<LlmBudgetExhaustedException>(() => budget.TryConsume(gameId));
        Assert.Equal(gameId, ex.GameId);
        Assert.Equal(200, ex.Max);
    }

    [Fact]
    public void Counts_are_isolated_per_game()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 5);
        var gameA = GameId.New();
        var gameB = GameId.New();

        budget.TryConsume(gameA);
        budget.TryConsume(gameA);
        budget.TryConsume(gameB);

        Assert.Equal(2, budget.Used(gameA));
        Assert.Equal(1, budget.Used(gameB));
    }

    [Fact]
    public void Used_returns_zero_for_unknown_game()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 5);

        Assert.Equal(0, budget.Used(GameId.New()));
    }

    [Fact]
    public async Task Concurrent_increments_are_safe_and_exact()
    {
        var budget = new GameLlmBudget(maxCallsPerGame: 1000);
        var gameId = GameId.New();

        var tasks = Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => budget.TryConsume(gameId)))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(500, budget.Used(gameId));
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_max()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameLlmBudget(maxCallsPerGame: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameLlmBudget(maxCallsPerGame: -1));
    }
}