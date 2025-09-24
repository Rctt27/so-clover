using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class InMemoryEventPublisher : IEventPublisher
{
    public Task Publish<TEvent>(TEvent evt, CancellationToken ct = default)
    {
        // For now just write to console; can be replaced by real bus later
        Console.WriteLine($"EVENT: {typeof(TEvent).Name} -> {evt}");
        return Task.CompletedTask;
    }
}
