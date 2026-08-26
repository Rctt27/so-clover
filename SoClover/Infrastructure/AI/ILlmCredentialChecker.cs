namespace SoClover.Infrastructure.AI;

/// <summary>
/// Verdict d'une sonde de credential LLM. Trois états et non un booléen : une panne réseau
/// n'est PAS une preuve de révocation, et les confondre grillerait la feature au moindre blip.
/// </summary>
public enum LlmCredentialStatus
{
    /// <summary>Le provider a répondu positivement — la clé est utilisable.</summary>
    Valid = 0,

    /// <summary>Le provider a rejeté la clé (401/403) — preuve d'une clé révoquée ou invalide.</summary>
    Invalid = 1,

    /// <summary>Timeout, DNS, 5xx… — aucune conclusion possible sur la validité de la clé.</summary>
    Indeterminate = 2
}

/// <summary>
/// Sonde bas niveau : interroge le provider pour savoir si la clé API configurée est encore
/// acceptée. L'implémentation retenue par provider est choisie dans <c>Program.cs</c>, en miroir
/// du switch de <see cref="ChatClientFactory"/> et de celui de
/// <c>IReasoningRequestConfigurator</c>.
/// </summary>
public interface ILlmCredentialChecker
{
    Task<LlmCredentialStatus> CheckAsync(CancellationToken ct = default);
}
