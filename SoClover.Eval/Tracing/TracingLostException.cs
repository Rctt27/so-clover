namespace SoClover.Eval.Tracing;

/// <summary>
/// Levée par <see cref="TracingSession.Checkpoint"/> quand une exportation a échoué depuis le
/// dernier checkpoint : la politique de la phase 3 (P2) veut qu'une perte de télémétrie arrête le
/// run avant que le verbe n'écrive les lignes de l'unité concernée.
/// </summary>
public sealed class TracingLostException(string unit, string detail) : Exception(
    $"traces perdues sur {unit} ({detail}) : reprendre avec la même commande. " +
    "Rien n'a été écrit dans l'artefact pour cette unité ; --trace off pour lancer sans traçage.");
