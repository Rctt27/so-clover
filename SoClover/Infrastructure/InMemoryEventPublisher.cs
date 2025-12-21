using System.Collections.Concurrent;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<object> _events = new();

    public IEnumerable<object> PublishedEvents => _events;

    public Task Publish<TEvent>(TEvent evt, CancellationToken ct = default)
    {
        if (evt != null)
        {
            _events.Enqueue(evt);
        }
        
        // For now just write to console; can be replaced by real bus later
        Console.WriteLine($"EVENT: {typeof(TEvent).Name} -> {evt}");
        return Task.CompletedTask;
    }
}
