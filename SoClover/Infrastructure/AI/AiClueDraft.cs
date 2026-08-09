using System.Text.Json.Serialization;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Un indice produit par le LLM pour une direction.
/// <paramref name="Candidates"/> est le scratchpad structuré demandé par le prompt FR v5
/// (chaînes de la forme <c>"Mot (fort, faible)"</c>). Nullable et non <c>[]</c> par défaut :
/// un modèle qui n'émet pas le champ doit rester distinguable d'un modèle qui émet une liste
/// vide. Aucun impact runtime — le champ était jusqu'ici ignoré par System.Text.Json.
/// </summary>
public sealed record AiClueDraft(
    [property: JsonPropertyName("direction")]   string Direction,
    [property: JsonPropertyName("clueWord")]    string ClueWord,
    [property: JsonPropertyName("explanation")] string Explanation,
    [property: JsonPropertyName("candidates")]  IReadOnlyList<string>? Candidates = null);
