using System.Text.Json.Serialization;

namespace SoClover.Infrastructure.AI;

public sealed record AiClueDraft(
    [property: JsonPropertyName("direction")]   string Direction,
    [property: JsonPropertyName("clueWord")]    string ClueWord,
    [property: JsonPropertyName("explanation")] string Explanation);
