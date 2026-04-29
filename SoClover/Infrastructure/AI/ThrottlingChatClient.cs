using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Decorator that gates IChatClient calls through a process-wide semaphore so the
/// total number of concurrent in-flight LLM requests (across all games / all AIs)
/// never exceeds <c>MaxConcurrency</c>. Must be registered as a SINGLETON: a Scoped
/// registration would create one semaphore per scope, defeating the purpose.
/// </summary>
public sealed class ThrottlingChatClient : DelegatingChatClient
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;

    public ThrottlingChatClient(IChatClient innerClient, int maxConcurrency)
        : base(innerClient)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                maxConcurrency,
                "MaxConcurrency must be >= 1.");
        }
        _maxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public int MaxConcurrency => _maxConcurrency;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _semaphore.Dispose();
        }
        base.Dispose(disposing);
    }
}