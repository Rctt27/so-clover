using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SoClover.Infrastructure.Persistence;

namespace SoClover.Infrastructure;

public sealed class CleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retention;

    public CleanupHostedService(IServiceProvider services)
    {
        _services = services;
        _interval = TimeSpan.FromHours(1);
        var hours = Environment.GetEnvironmentVariable("GAMES_RETENTION_HOURS");
        if (!int.TryParse(hours, out var h) || h <= 0) h = 72;
        _retention = TimeSpan.FromHours(h);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                var cutoff = DateTime.UtcNow - _retention;
                var oldCompleted = await db.Games
                    .Where(g => g.Status == "Completed" && g.UpdatedAtUtc < cutoff)
                    .ToListAsync(stoppingToken);
                if (oldCompleted.Count > 0)
                {
                    db.Games.RemoveRange(oldCompleted);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch
            {
                // swallow on background; could log
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }
    }
}