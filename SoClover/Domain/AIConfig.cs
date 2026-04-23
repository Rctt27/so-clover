namespace SoClover.Domain;

/// <summary>
/// Configuration d'un joueur AI : modèle LLM et température d'échantillonnage.
/// Record positionnel : sérialisation System.Text.Json native depuis .NET 6.
/// </summary>
public sealed record AIConfig(string Model, double Temperature);
