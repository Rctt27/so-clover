namespace SoClover.Domain;

public sealed class CloverBoard
{
    // A 2x2 clover board with four positions around center
    public OrientedCard? Top { get; private set; }
    public OrientedCard? Right { get; private set; }
    public OrientedCard? Bottom { get; private set; }
    public OrientedCard? Left { get; private set; }

    public ClueText? TopClue { get; private set; }
    public ClueText? RightClue { get; private set; }
    public ClueText? BottomClue { get; private set; }
    public ClueText? LeftClue { get; private set; }

    public void Place(Direction direction, OrientedCard orientedCard)
    {
        switch (direction)
        {
            case Direction.Top:
                Top = orientedCard; break;
            case Direction.Right:
                Right = orientedCard; break;
            case Direction.Bottom:
                Bottom = orientedCard; break;
            case Direction.Left:
                Left = orientedCard; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    public void Rotate(Direction direction)
    {
        switch (direction)
        {
            case Direction.Top:
                Top = Top?.RotateRight(); break;
            case Direction.Right:
                Right = Right?.RotateRight(); break;
            case Direction.Bottom:
                Bottom = Bottom?.RotateRight(); break;
            case Direction.Left:
                Left = Left?.RotateRight(); break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
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

    public string? GetVisibleWord(Direction direction)
    {
        return direction switch
        {
            Direction.Top => Top?.GetWord(Direction.Bottom),
            Direction.Right => Right?.GetWord(Direction.Left),
            Direction.Bottom => Bottom?.GetWord(Direction.Top),
            Direction.Left => Left?.GetWord(Direction.Right),
            _ => null
        };
    }
}