using SoClover.Domain;

namespace SoClover.UseCases.AI;

public readonly record struct AiClueGenerationRequested(GameId GameId, PlayerId PlayerId);

public readonly record struct AiClueGenerated(
    GameId GameId,
    PlayerId PlayerId,
    Direction Direction,
    string ClueText,
    string Explanation);

public readonly record struct AiClueGenerationFailed(
    GameId GameId,
    PlayerId PlayerId,
    Direction Direction,
    string Reason,
    IReadOnlyList<string> AttemptedClues);

public readonly record struct AiPlayerBoardFailed(
    GameId GameId,
    PlayerId PlayerId,
    string Reason);

/// <summary>
/// Progression transitoire de la génération d'indices IA pour un board, émise à chaque
/// tentative (succès ou rejet). Porte des compteurs absolus (pas des incréments) → robuste
/// face aux pertes/réordonnancements d'events, pas de désync côté client.
/// </summary>
public readonly record struct AiClueProgressUpdate(
    GameId GameId,
    PlayerId PlayerId,
    int CluesSubmitted,
    IReadOnlyDictionary<Direction, int> RetriesByDirection);
