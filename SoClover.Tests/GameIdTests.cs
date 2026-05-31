using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class GameIdTests
{
    [Fact]
    public void From_wraps_string_value()
    {
        var id = GameId.From("lamp-pear-house-sheep");
        Assert.Equal("lamp-pear-house-sheep", id.Value);
        Assert.Equal("lamp-pear-house-sheep", id.ToString());
    }

    [Fact]
    public void New_produces_unique_non_empty_ids()
    {
        var a = GameId.New();
        var b = GameId.New();
        Assert.NotEqual(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a.Value));
    }
}
