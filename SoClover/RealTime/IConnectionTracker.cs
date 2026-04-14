using SoClover.Domain;

namespace SoClover.RealTime;

/// <summary>
/// Tracks which players are currently connected via SignalR.
/// Abstracted so UseCase layer can depend on it without coupling to GameHub directly.
/// </summary>
public interface IConnectionTracker
{
    bool IsPlayerConnected(PlayerId playerId);
}

/// <summary>
/// Production implementation — delegates to GameHub's static connection map.
/// </summary>
public sealed class SignalRConnectionTracker : IConnectionTracker
{
    public bool IsPlayerConnected(PlayerId playerId) =>
        GameHub.IsPlayerConnected(playerId);
}
