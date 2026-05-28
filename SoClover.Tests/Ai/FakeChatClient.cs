using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SoClover.Tests.AI;

/// <summary>
/// Scriptable test double for IChatClient. Enqueue responses (or exceptions) before
/// invoking; each call dequeues exactly one. Records start/end timestamps so tests
/// can assert that calls were serialized (throttling) or canceled within a deadline (timeout).
/// Thread-safe: the queue and log are concurrent collections.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly ConcurrentQueue<ScriptedResponse> _queue = new();
    private readonly ConcurrentBag<CallRecord> _calls = new();

    public IReadOnlyList<CallRecord> CallLog =>
        _calls.OrderBy(c => c.Start).ToList();

    public int CallCount => _calls.Count;

    /// <summary>Options passed to the most recent GetResponseAsync call (for asserting sampling/params).</summary>
    public ChatOptions? LastOptions { get; private set; }

    /// <summary>Enqueues a string response, optionally delayed to simulate latency.</summary>
    public void Enqueue(string text, TimeSpan? artificialDelay = null)
    {
        _queue.Enqueue(new ScriptedResponse(text, artificialDelay, ExceptionToThrow: null));
    }

    /// <summary>Enqueues an exception that will be thrown by the next call.</summary>
    public void EnqueueException(Exception ex)
    {
        _queue.Enqueue(new ScriptedResponse(Text: null, Delay: null, ExceptionToThrow: ex));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow;
        LastOptions = options;

        if (!_queue.TryDequeue(out var scripted))
        {
            _calls.Add(new CallRecord(start, DateTime.UtcNow));
            throw new InvalidOperationException(
                "FakeChatClient: no scripted response. Call Enqueue(...) before invoking.");
        }

        try
        {
            if (scripted.Delay is { } delay)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (scripted.ExceptionToThrow is { } ex)
            {
                throw ex;
            }

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, scripted.Text!));
            return response;
        }
        finally
        {
            _calls.Add(new CallRecord(start, DateTime.UtcNow));
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var msg in response.Messages)
        {
            yield return new ChatResponseUpdate(msg.Role, msg.Text);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { /* nothing to dispose */ }

    private sealed record ScriptedResponse(string? Text, TimeSpan? Delay, Exception? ExceptionToThrow);

    public sealed record CallRecord(DateTime Start, DateTime End)
    {
        public TimeSpan Duration => End - Start;
    }
}