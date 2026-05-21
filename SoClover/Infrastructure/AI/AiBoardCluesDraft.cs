using System.Text.Json.Serialization;

namespace SoClover.Infrastructure.AI;

public sealed record AiBoardCluesDraft(
    [property: JsonPropertyName("clues")] IReadOnlyList<AiClueDraft> Clues);
