using Microsoft.Extensions.AI;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class ThrottlingChatClientTests
{
    [Fact]
    public async Task Three_concurrent_calls_with_MaxConcurrency_1_serialize_completely()
    {
        var fake = new FakeChatClient();
        // Each call takes ~100ms — if not serialized, total wall clock ≈ 100ms;
        // if serialized, total wall clock ≈ 300ms.
        fake.Enqueue("r1", artificialDelay: TimeSpan.FromMilliseconds(100));
        fake.Enqueue("r2", artificialDelay: TimeSpan.FromMilliseconds(100));
        fake.Enqueue("r3", artificialDelay: TimeSpan.FromMilliseconds(100));

        using var throttled = new ThrottlingChatClient(fake, maxConcurrency: 1);

        var tasks = Enumerable.Range(0, 3)
            .Select(_ => throttled.GetResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "x") }))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(3, fake.CallCount);

        // Assert: no two call windows overlap (serialized).
        var windows = fake.CallLog;
        for (int i = 1; i < windows.Count; i++)
        {
            // Allow a small scheduling tolerance.
            var tolerance = TimeSpan.FromMilliseconds(10);
            Assert.True(
                windows[i].Start >= windows[i - 1].End - tolerance,
                $"Call {i} started at {windows[i].Start:O} but previous ended at {windows[i - 1].End:O} — calls overlapped, throttling broken.");
        }
    }

    [Fact]
    public async Task Two_concurrent_calls_with_MaxConcurrency_2_run_in_parallel()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("r1", artificialDelay: TimeSpan.FromMilliseconds(100));
        fake.Enqueue("r2", artificialDelay: TimeSpan.FromMilliseconds(100));

        using var throttled = new ThrottlingChatClient(fake, maxConcurrency: 2);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => throttled.GetResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "x") }))
            .ToArray();
        await Task.WhenAll(tasks);
        sw.Stop();

        // If parallelized, total ≈ 100ms; if serialized, ≈ 200ms.
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(180),
            $"Calls did not run in parallel: total elapsed = {sw.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task Releases_semaphore_when_inner_throws()
    {
        var fake = new FakeChatClient();
        fake.EnqueueException(new InvalidOperationException("boom"));
        fake.Enqueue("ok");

        using var throttled = new ThrottlingChatClient(fake, maxConcurrency: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            throttled.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "x") }));

        // If the semaphore wasn't released, this second call would deadlock.
        var response = await throttled.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "x") });

        Assert.Equal("ok", response.Text);
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_concurrency()
    {
        var fake = new FakeChatClient();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottlingChatClient(fake, maxConcurrency: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThrottlingChatClient(fake, maxConcurrency: -1));
    }
}