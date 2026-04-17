using System.Text.Json.Serialization;

namespace SoClover.Domain;

public sealed class CloverBoard
{
    // A 2x2 clover board with four corner positions
    [JsonInclude]
    [JsonPropertyName("topLeft")]
    public OrientedCard? TopLeft { get; private set; }

    [JsonInclude]
    [JsonPropertyName("topRight")]
    public OrientedCard? TopRight { get; private set; }

    [JsonInclude]
    [JsonPropertyName("bottomRight")]
    public OrientedCard? BottomRight { get; private set; }

    [JsonInclude]
    [JsonPropertyName("bottomLeft")]
    public OrientedCard? BottomLeft { get; private set; }

    [JsonInclude]
    [JsonPropertyName("topClue")]
    public ClueText? TopClue { get; private set; }

    [JsonInclude]
    [JsonPropertyName("rightClue")]
    public ClueText? RightClue { get; private set; }

    [JsonInclude]
    [JsonPropertyName("bottomClue")]
    public ClueText? BottomClue { get; private set; }

    [JsonInclude]
    [JsonPropertyName("leftClue")]
    public ClueText? LeftClue { get; private set; }

    // Explicit submission marker for WritingClues phase
    [JsonInclude]
    [JsonPropertyName("isSubmitted")]
    public bool IsSubmitted { get; private set; }

    [JsonInclude]
    [JsonPropertyName("submittedAtUtc")]
    public DateTime? SubmittedAtUtc { get; private set; }

    // Track guessed edges and persist across EF JSON round-trips
    [JsonInclude]
    [JsonPropertyName("guessedDirections")]
    public HashSet<Direction> GuessedDirections { get; private set; } = new();

    public void Place(BoardPosition position, OrientedCard orientedCard)
    {
        switch (position)
        {
            case BoardPosition.TopLeft:
                TopLeft = orientedCard; break;
            case BoardPosition.TopRight:
                TopRight = orientedCard; break;
            case BoardPosition.BottomRight:
                BottomRight = orientedCard; break;
            case BoardPosition.BottomLeft:
                BottomLeft = orientedCard; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(position));
        }
    }

    // Convenience overload: map directions to a default corner on that edge
    public void Place(Direction direction, OrientedCard orientedCard)
    {
        var position = direction switch
        {
            Direction.Top => BoardPosition.TopLeft,
            Direction.Right => BoardPosition.TopRight,
            Direction.Bottom => BoardPosition.BottomRight,
            Direction.Left => BoardPosition.BottomLeft,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        Place(position, orientedCard);
    }

    public void SetClue(Direction direction, ClueText clue)
    {
        switch (direction)
        {
            case Direction.Top:
                TopClue = clue; break;
            case Direction.Right:
                RightClue = clue; break;
            case Direction.Bottom:
                BottomClue = clue; break;
            case Direction.Left:
                LeftClue = clue; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
    
    // Used when the server invalidates an existing clue by replacing it with an invalid one
    public void ClearClue(Direction direction)
    {
        switch (direction)
        {
            case Direction.Top:
                TopClue = null; break;
            case Direction.Right:
                RightClue = null; break;
            case Direction.Bottom:
                BottomClue = null; break;
            case Direction.Left:
                LeftClue = null; break;
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    // Mark the board as explicitly submitted by the player (idempotent, irreversible during the round)
    public void MarkSubmitted(DateTime nowUtc)
    {
        if (!IsSubmitted)
        {
            IsSubmitted = true;
            SubmittedAtUtc = nowUtc;
        }
    }

    // Clear submission status (intended for start of a new round/phase only)
    public void ResetSubmission()
    {
        IsSubmitted = false;
        SubmittedAtUtc = null;
    }

    public string GetClueText(Direction direction)
    {
        var result = direction switch
        {
            // Pick one corner on that edge deterministically
            Direction.Top => TopLeft?.GetWord(Direction.Bottom),
            Direction.Right => TopRight?.GetWord(Direction.Left),
            Direction.Bottom => BottomRight?.GetWord(Direction.Top),
            Direction.Left => BottomLeft?.GetWord(Direction.Right),
            _ => null
        };

        if (result is null)
            throw new NoClueForDirectionException(direction);
        return result;
    }

    public void MarkGuessed(Direction direction)
    {
        GuessedDirections.Add(direction);
    }

    public bool IsDirectionGuessed(Direction direction) => GuessedDirections.Contains(direction);

    public bool IsComplete() => GuessedDirections.Count >= 4;
}