namespace SoClover.Infrastructure.AI;

/// <summary>
/// Décision mémoïsée « la clé API LLM est-elle disponible ? ». Ne dit rien du feature flag
/// <c>AIPlayers.Enabled</c> : les appelants composent les deux (le <c>&amp;&amp;</c> de C#
/// court-circuite la sonde quand le flag est à false).
/// </summary>
public interface ILlmApiKeyProbe
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
