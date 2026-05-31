using System;

namespace SoClover.Infrastructure.Persistence;

public sealed class GameEntity
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Language { get; set; }
    public DateTime? PhaseEndsAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty; // Full aggregate snapshot (jsonb)
    // Uses PostgreSQL xmin system column for optimistic concurrency via model config
}