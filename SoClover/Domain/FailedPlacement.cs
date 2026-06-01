using System.Text.Json.Serialization;

namespace SoClover.Domain;

/// <summary>
/// Trace d'un placement (carte + position + rotation) tenté et invalidé pendant la phase
/// Guessing du board courant. Réinitialisé à chaque changement de board.
/// La rotation fait partie de l'identité du placement : un même couple carte+position avec une rotation différente constitue une entrée distincte — c'est intentionnel.
/// </summary>
public sealed record FailedPlacement(
    [property: JsonPropertyName("position")] BoardPosition Position,
    [property: JsonPropertyName("cardId")] Guid CardId,
    [property: JsonPropertyName("rotation")] Rotation Rotation
);
