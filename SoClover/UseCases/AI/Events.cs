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
