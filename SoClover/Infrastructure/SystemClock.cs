using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}