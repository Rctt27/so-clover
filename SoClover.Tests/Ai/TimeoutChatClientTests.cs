using Microsoft.Extensions.AI;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class TimeoutChatClientTests
{
    [Fact]
    public async Task Returns_response_when_inner_completes_before_timeout()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("ok", artificialDelay: TimeSpan.FromMilliseconds(50));

        using var timed = new TimeoutChatClient(fake, timeout: TimeSpan.FromSeconds(2));

        var response = await timed.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "x") });

        Assert.Equal("ok", response.Text);
    }

    [Fact]
    public async Task Cancels_when_inner_exceeds_timeout()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("never-returned", artificialDelay: TimeSpan.FromSeconds(5));

        using var timed = new TimeoutChatClient(fake, timeout: TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            timed.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "x") }));
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_to_inner()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("never-returned", artificialDelay: TimeSpan.FromSeconds(5));

        using var timed = new TimeoutChatClient(fake, timeout: TimeSpan.FromSeconds(60));
        using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            timed.GetResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "x") },
                cancellationToken: callerCts.Token));
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_timeout()
    {
        var fake = new FakeChatClient();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimeoutChatClient(fake, timeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TimeoutChatClient(fake, timeout: TimeSpan.FromSeconds(-1)));
    }
}