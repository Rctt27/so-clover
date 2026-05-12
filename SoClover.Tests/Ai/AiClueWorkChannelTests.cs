using SoClover.Domain;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueWorkChannelTests
{
    [Fact]
    public async Task Write_then_Read_returns_same_message()
    {
        var channel = new AiClueWorkChannel();
        var gameId = GameId.New();
        var playerId = PlayerId.New();
        var msg = new AiClueGenerationRequested(gameId, playerId);

        await channel.Writer.WriteAsync(msg);
        var ok = channel.Reader.TryRead(out var received);

        Assert.True(ok);
        Assert.Equal(gameId, received.GameId);
        Assert.Equal(playerId, received.PlayerId);
    }

    [Fact]
    public async Task ReadAllAsync_yields_each_written_message_in_order()
    {
        var channel = new AiClueWorkChannel();
        var gameId = GameId.New();
        var p1 = PlayerId.New();
        var p2 = PlayerId.New();

        await channel.Writer.WriteAsync(new AiClueGenerationRequested(gameId, p1));
        await channel.Writer.WriteAsync(new AiClueGenerationRequested(gameId, p2));
        channel.Writer.Complete();

        var received = new List<PlayerId>();
        await foreach (var msg in channel.Reader.ReadAllAsync())
        {
            received.Add(msg.PlayerId);
        }

        Assert.Equal(new[] { p1, p2 }, received);
    }
}
