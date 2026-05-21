using SoClover.Domain;

namespace SoClover.Infrastructure.AI.Prompts;

public interface IAiCluePromptProvider
{
    string Language { get; }
    AiCluePromptBundle BuildBoardCluesPrompt(BoardCluesPromptContext context);
}

/// <summary>
/// One card of the 2x2 clover board, with its 4 oriented words.
/// Position is one of TopLeft / TopRight / BottomRight / BottomLeft.
/// </summary>
public readonly record struct BoardCardSnapshot(
    BoardPosition Position,
    string TopWord,
    string RightWord,
    string BottomWord,
    string LeftWord);

/// <summary>
/// One previous rejected clue attempt for a given direction.
/// The direction itself is the key in <see cref="BoardCluesPromptContext.RejectedPerDirection"/>.
/// </summary>
public readonly record struct RejectedAttempt(
    string ClueText,
    string RejectionReason);

/// <summary>
/// Pure input for the prompt provider. Carries the full board layout, the directions still
/// to resolve (subset of the 4) and, per direction, the history of rejected attempts.
/// </summary>
/// <remarks>
/// Invariants enforced by the provider (throws ArgumentException otherwise):
/// - Cards must contain exactly 4 entries, one per BoardPosition.
/// - RemainingDirections must be non-empty and a subset of { Top, Right, Bottom, Left }.
/// - RejectedPerDirection keys must all be in RemainingDirections (no rejects logged for a
///   direction that's already resolved).
/// </remarks>
public readonly record struct BoardCluesPromptContext(
    string Language,
    IReadOnlyList<BoardCardSnapshot> Cards,
    IReadOnlyList<Direction> RemainingDirections,
    IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>> RejectedPerDirection);

public readonly record struct AiCluePromptBundle(
    string SystemPrompt,
    string UserPrompt,
    string JsonSchema,
    int? PromptVersion = null);
