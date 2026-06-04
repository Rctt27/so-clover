using SoClover.Domain;

namespace SoClover.RealTime;

/// <summary>
/// Durée de la période de grâce après déconnexion, fonction de la phase.
/// Plus longue en Lobby (mobile : on copie/partage le code en arrière-plan).
/// </summary>
public static class GracePeriodPolicy
{
    public const int LobbyGraceSeconds = 60;
    public const int InGameGraceSeconds = 45;

    public static int SecondsForPhase(GamePhase phase) =>
        phase == GamePhase.Lobby ? LobbyGraceSeconds : InGameGraceSeconds;
}
