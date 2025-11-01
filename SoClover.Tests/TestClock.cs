using SoClover.UseCases.Abstractions;

namespace SoClover.Tests;

public sealed class TestClock : IClock
{
    private DateTime _utcNow;

    public TestClock(DateTime utcNow)
    {
        _utcNow = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
    public void Set(DateTime utcNow) => _utcNow = utcNow;
}