namespace SoClover.Infrastructure.AI;

/// <summary>
/// Checker no-op utilisé pour le provider OpenAI-compatible (LM Studio en Development).
///
/// Décision assumée : la sonde ne concerne que le provider Anthropic, dont la clé est
/// révocable à la demande depuis la console. Sonder LM Studio griserait le bouton « ajouter
/// un joueur IA » tant que le serveur local n'est pas chargé — une régression pénible pendant
/// les séances d'évaluation, pour un levier qui n'existe pas de ce côté.
/// </summary>
public sealed class AlwaysValidLlmCredentialChecker : ILlmCredentialChecker
{
    public Task<LlmCredentialStatus> CheckAsync(CancellationToken ct = default)
        => Task.FromResult(LlmCredentialStatus.Valid);
}
