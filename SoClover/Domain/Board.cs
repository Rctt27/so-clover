namespace SoClover.Domain;

public sealed class CloverBoard
{
    // A 2x2 clover board with four corner positions
    public OrientedCard? TopLeft { get; private set; }
    public OrientedCard? TopRight { get; private set; }
    public OrientedCard? BottomRight { get; private set; }
    public OrientedCard? BottomLeft { get; private set; }

    public ClueText? TopClue { get; private set; }
    public ClueText? RightClue { get; private set; }
    public ClueText? BottomClue { get; private set; }
    public ClueText? LeftClue { get; private set; }

    private readonly HashSet<Direction> _guessedDirections = new();

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
        _guessedDirections.Add(direction);
    }

    public bool IsDirectionGuessed(Direction direction) => _guessedDirections.Contains(direction);

    public bool IsComplete() => _guessedDirections.Count >= 4;
}