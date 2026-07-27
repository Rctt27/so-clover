using Microsoft.Extensions.AI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Base des échecs d'un appel LLM de génération d'indice. Dérive de
/// <see cref="InvalidOperationException"/> pour préserver le <c>catch</c> existant des pipelines
/// de production. Porte l'observabilité de l'appel (latence, version de prompt, modèle effectif)
/// afin que l'appelant puisse journaliser l'appel échoué exactement comme un appel réussi.
/// </summary>
public abstract class LlmCallException : InvalidOperationException
{
    protected LlmCallException(string message, Exception? inner = null) : base(message, inner) { }

    public long LatencyMs { get; init; }
    public int? PromptVersion { get; init; }
    public string EffectiveModel { get; init; } = string.Empty;

    /// <summary>
    /// Diagnostic non fatal de chargement du préambule reasoning, rencontré sur CET appel avant son
    /// échec. Porté ici pour que l'appelant puisse le journaliser même quand l'appel ne retourne pas
    /// de <c>ClueCallResult</c> — <see cref="AiClueLlmCaller"/> ne journalise jamais lui-même.
    /// </summary>
    public ReasoningPreambleWarning? PreambleWarning { get; init; }
}

/// <summary>
/// Le modèle a répondu sans contenu textuel. Cas typique d'un modèle reasoning dont la réflexion
/// native sature le budget de sortie avant l'émission du JSON (<c>finish_reason=length</c>).
/// </summary>
public sealed class EmptyLlmResponseException : LlmCallException
{
    public EmptyLlmResponseException(
        ChatFinishReason? finishReason, long? inputTokens, long? outputTokens)
        : base("LLM returned an empty response.")
    {
        FinishReason = finishReason;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    public ChatFinishReason? FinishReason { get; }
    public long? InputTokens { get; }
    public long? OutputTokens { get; }
}

/// <summary>
/// Le modèle a répondu, mais le texte (après strip des balises de réflexion et des fences) n'est
/// pas du JSON exploitable.
/// </summary>
public sealed class UnparseableLlmResponseException : LlmCallException
{
    public const int MaxExcerptLength = 500;

    public UnparseableLlmResponseException(string rawText, Exception? inner = null)
        : base("LLM returned invalid JSON.", inner)
    {
        RawTextExcerpt = Truncate(rawText);
    }

    public string RawTextExcerpt { get; }

    private static string Truncate(string? text)
    {
        var t = text ?? string.Empty;
        return t.Length <= MaxExcerptLength ? t : t[..MaxExcerptLength];
    }
}
