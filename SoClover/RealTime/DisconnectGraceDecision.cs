using SoClover.Domain;

namespace SoClover.RealTime;

public enum GraceAction
{
    None,
    LeaveGame,
    DisconnectPlayer,
}

/// <summary>
/// Décide de l'action à mener quand la période de grâce expire sans reconnexion.
/// Garde-fou : en Lobby, l'admin n'est jamais auto-retiré (seul un kick explicite le sort).
/// </summary>
public static class DisconnectGraceDecision
{
    public static GraceAction Decide(GamePhase phase, bool isAdmin) => phase switch
    {
        GamePhase.Lobby when isAdmin => GraceAction.None,
        GamePhase.Lobby => GraceAction.LeaveGame,
        GamePhase.WritingClues => GraceAction.DisconnectPlayer,
        _ => GraceAction.None,
    };
}
