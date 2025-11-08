using System;

namespace SoClover.Infrastructure.Persistence;

/// <summary>
/// Persisted scoring result per player for a given game.
/// Captures the information displayed in the Scoring phase UI.
/// </summary>
public sealed class GameResultEntity
{
    public Guid Id { get; set; } // row identity
    public Guid GameId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public bool BoardIsGuessed { get; set; }
    public int Attempts { get; set; }
    public int DurationSeconds { get; set; }
}
