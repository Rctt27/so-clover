using Microsoft.Extensions.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class FakeChatClientSmokeTests
{
    [Fact]
    public async Task Returns_enqueued_text_response()
    {
        using var fake = new FakeChatClient();
        fake.Enqueue("hello");

        var response = await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });

        Assert.Equal("hello", response.Text);
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task Throws_enqueued_exception()
    {
        using var fake = new FakeChatClient();
        fake.EnqueueException(new InvalidOperationException("boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Logs_start_and_end_per_call()
    {
        using var fake = new FakeChatClient();
        fake.Enqueue("a", artificialDelay: TimeSpan.FromMilliseconds(50));

        await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });

        var record = Assert.Single(fake.CallLog);
        Assert.True(record.Duration >= TimeSpan.FromMilliseconds(40),
            $"Duration was {record.Duration.TotalMilliseconds}ms — expected ≥40ms.");
    }

    [Fact]
    public async Task Throws_when_no_response_enqueued()
    {
        using var fake = new FakeChatClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }));
    }
}