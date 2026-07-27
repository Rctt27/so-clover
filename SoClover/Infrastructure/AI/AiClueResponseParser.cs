using System.Text.Json;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Parse des réponses d'indices du LLM, partagé entre la production et le harnais d'évaluation
/// hors ligne — pour que les deux aient exactement la même tolérance de format.
/// </summary>
public static class AiClueResponseParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Pipeline PerDirection. Accepte le format mono-clue (canonique) OU le format enveloppé
    /// <c>{ clues: [...] }</c> (compat tests et modèles non-stricts).
    /// </summary>
    public static AiBoardCluesDraft ParseSingleDirection(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("clues", out _))
            {
                var wrapped = JsonSerializer.Deserialize<AiBoardCluesDraft>(text, Options)
                    ?? throw new UnparseableLlmResponseException(text);
                if (wrapped.Clues is null)
                    throw new UnparseableLlmResponseException(text);
                return wrapped;
            }

            var item = JsonSerializer.Deserialize<AiClueDraft>(text, Options)
                ?? throw new UnparseableLlmResponseException(text);
            return new AiBoardCluesDraft([item]);
        }
        catch (JsonException ex)
        {
            throw new UnparseableLlmResponseException(text, ex);
        }
    }

    /// <summary>Pipeline PerBoard : uniquement le format enveloppé <c>{ clues: [...] }</c>.</summary>
    public static AiBoardCluesDraft ParseBoard(string text)
    {
        try
        {
            var draft = JsonSerializer.Deserialize<AiBoardCluesDraft>(text, Options)
                ?? throw new UnparseableLlmResponseException(text);
            if (draft.Clues is null)
                throw new UnparseableLlmResponseException(text);
            return draft;
        }
        catch (JsonException ex)
        {
            throw new UnparseableLlmResponseException(text, ex);
        }
    }
}
