using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SoClover.Tests.AI;

public sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<LogRecord> Records { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var props = new Dictionary<string, object?>();
        if (state is IReadOnlyList<KeyValuePair<string, object?>> kvps)
        {
            foreach (var kv in kvps)
            {
                if (kv.Key == "{OriginalFormat}") continue;
                props[kv.Key] = kv.Value;
            }
        }
        else if (state is IReadOnlyList<KeyValuePair<string, object>> kvps2)
        {
            foreach (var kv in kvps2)
            {
                if (kv.Key == "{OriginalFormat}") continue;
                props[kv.Key] = kv.Value;
            }
        }
        Records.Enqueue(new LogRecord(logLevel, eventId, message, props, exception));
    }
}

public sealed record LogRecord(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);
