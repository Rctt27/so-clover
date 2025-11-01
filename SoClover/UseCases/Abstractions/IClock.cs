namespace SoClover.UseCases.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}