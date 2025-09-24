namespace SoClover.UseCases.Abstractions;

public interface IEventPublisher
{
    Task Publish<TEvent>(TEvent evt, CancellationToken ct = default);
}
