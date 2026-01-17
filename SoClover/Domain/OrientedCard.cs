using System.Text.Json.Serialization;

namespace SoClover.Domain;

public sealed class OrientedCard
{
    public Card Card { get; }
    public Rotation Rotation { get; }

    [JsonConstructor]
    public OrientedCard(Card card, Rotation rotation = Rotation.None)
    {
        Card = card;
        Rotation = rotation;
    }

    public string GetWord(Direction direction)
    {
        var rotated = Rotate(direction, Rotation);
        return rotated switch
        {
            Direction.Top => Card.TopWord,
            Direction.Right => Card.RightWord,
            Direction.Bottom => Card.BottomWord,
            Direction.Left => Card.LeftWord,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }

    public OrientedCard RotateRight()
    {
        var next = Rotation switch
        {
            Rotation.None => Rotation.Right90,
            Rotation.Right90 => Rotation.Right180,
            Rotation.Right180 => Rotation.Right270,
            Rotation.Right270 => Rotation.None,
            _ => Rotation.None
        };
        return new OrientedCard(Card, next);
    }

    public OrientedCard RotateLeft()
    {
        var next = Rotation switch
        {
            Rotation.None => Rotation.Right270,
            Rotation.Right90 => Rotation.None,
            Rotation.Right180 => Rotation.Right90,
            Rotation.Right270 => Rotation.Right180,
            _ => Rotation.None
        };
        return new OrientedCard(Card, next);
    }

    public OrientedCard Rotate(int steps)
    {
        int current = (int)Rotation;
        int next = (current + steps) % 4;
        if (next < 0) next += 4;
        return new OrientedCard(Card, (Rotation)next);
    }

    private static Direction Rotate(Direction direction, Rotation rotation)
    {
        var offset = (int)rotation;
        var dir = (int)direction;
        var rotated = (dir - offset) & 0b11; // wrap 0..3
        return (Direction)rotated;
    }
}