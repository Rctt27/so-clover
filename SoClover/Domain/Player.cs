namespace SoClover.Domain;

public sealed class Player
{
    public PlayerId Id { get; }
    public string Name { get; }
    public CloverBoard Board { get; } = new();

    public Player(PlayerId id, string name)
    {
        Id = id;
        Name = RequireName(name);
    }

    private static string RequireName(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new DomainException("Player name cannot be empty.");
        if (value.Length > 32)
            throw new DomainException("Player name cannot exceed 32 characters.");
        return value;
    }
}