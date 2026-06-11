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
/// Production implementation — delegates to the shared <see cref="PlayerConnectionRegistry"/>.
/// </summary>
public sealed class SignalRConnectionTracker : IConnectionTracker
{
    private readonly PlayerConnectionRegistry _registry;

    public SignalRConnectionTracker(PlayerConnectionRegistry registry) => _registry = registry;

    public bool IsPlayerConnected(PlayerId playerId) =>
        _registry.IsConnected(playerId.Value.ToString());
}
