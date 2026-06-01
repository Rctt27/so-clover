using System.Text.Json.Serialization;

namespace SoClover.Domain;

/// <summary>
/// Trace d'un placement (carte + position + rotation) tenté et invalidé pendant la phase
/// Guessing du board courant. Réinitialisé à chaque changement de board.
/// </summary>
public sealed record FailedPlacement(
    [property: JsonPropertyName("position")] BoardPosition Position,
    [property: JsonPropertyName("cardId")] Guid CardId,
    [property: JsonPropertyName("rotation")] Rotation Rotation
);
