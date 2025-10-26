namespace SoClover.Domain;

public readonly record struct GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct CardId(Guid Value)
{
    public static CardId New() => new(Guid.NewGuid());
    public static CardId Create() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct ClueText
{
    public string Value { get; }

    private ClueText(string value)
    {
        Value = value;
    }

    public static ClueText Create(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new InvalidClueException("Clue cannot be empty.");
        if (value.Length > 32)
            throw new InvalidClueException("Clue cannot exceed 32 characters.");
        return new ClueText(value);
    }

    public override string ToString() => Value;
}


