namespace SoClover.Domain;

public sealed class Player
{
    public PlayerId Id { get; }
    public string Name { get; }
    public bool IsAdmin { get; }
    public CloverBoard Board { get; } = new();

    public Player(PlayerId id, string name, bool isAdmin = false)
    {
        Id = id;
        Name = RequireName(name);
        IsAdmin = isAdmin;
    }

    private static string RequireName(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new PlayerNameEmptyException();
        if (value.Length > 32)
            throw new PlayerNameTooLongException(32);
        return value;
    }
}