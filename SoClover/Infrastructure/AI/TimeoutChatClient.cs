using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Decorator that enforces a per-call deadline by combining the caller's CancellationToken
/// with a local CTS scheduled at <c>timeout</c>. When composed outside ThrottlingChatClient
/// (i.e. <c>Timeout(Throttle(Provider))</c>), the semaphore wait counts against the deadline,
/// providing safe backpressure: a request that waits too long for a slot fails fast instead
/// of starving downstream callers.
/// </summary>
public sealed class TimeoutChatClient : DelegatingChatClient
{
    private readonly TimeSpan _timeout;

    public TimeoutChatClient(IChatClient innerClient, TimeSpan timeout)
        : base(innerClient)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout must be > 0.");
        }
        _timeout = timeout;
    }

    public TimeSpan Timeout => _timeout;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        return await base.GetResponseAsync(messages, options, linkedCts.Token).ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, linkedCts.Token)
            .WithCancellation(linkedCts.Token)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }
}