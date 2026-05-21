using SoClover.Domain;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class InMemoryAiClueExplanationStoreTests
{
    [Fact]
    public void Save_then_GetFor_returns_saved_explanation()
    {
        var store = new InMemoryAiClueExplanationStore();
        var gameId = GameId.New();
        var playerId = PlayerId.New();

        store.Save(gameId, playerId, Direction.Top, "Mer + plage");

        Assert.Equal("Mer + plage", store.GetFor(gameId, playerId, Direction.Top));
    }

    [Fact]
    public void GetFor_returns_null_when_no_entry()
    {
        var store = new InMemoryAiClueExplanationStore();

        Assert.Null(store.GetFor(GameId.New(), PlayerId.New(), Direction.Top));
    }

    [Fact]
    public void Save_overwrites_existing_entry()
    {
        var store = new InMemoryAiClueExplanationStore();
        var gameId = GameId.New();
        var playerId = PlayerId.New();

        store.Save(gameId, playerId, Direction.Top, "v1");
        store.Save(gameId, playerId, Direction.Top, "v2");

        Assert.Equal("v2", store.GetFor(gameId, playerId, Direction.Top));
    }

    [Fact]
    public void GetAll_returns_only_entries_for_requested_game()
    {
        var store = new InMemoryAiClueExplanationStore();
        var gameA = GameId.New();
        var gameB = GameId.New();
        var pid = PlayerId.New();

        store.Save(gameA, pid, Direction.Top, "A-top");
        store.Save(gameA, pid, Direction.Right, "A-right");
        store.Save(gameB, pid, Direction.Top, "B-top");

        var all = store.GetAll(gameA);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Direction == Direction.Top && e.Explanation == "A-top");
        Assert.Contains(all, e => e.Direction == Direction.Right && e.Explanation == "A-right");
        Assert.DoesNotContain(all, e => e.Explanation == "B-top");
    }
}
