using System.Text.Json.Serialization;

namespace SoClover.Domain;

public sealed class Player
{
    [JsonInclude]
    [JsonPropertyName("id")]
    public PlayerId Id { get; private set; }

    [JsonInclude]
    [JsonPropertyName("name")]
    public string Name { get; private set; }

    [JsonInclude]
    [JsonPropertyName("isAdmin")]
    public bool IsAdmin { get; internal set; }

    // Ensure the board is persisted and rehydrated when using EF JSON snapshots
    [JsonInclude]
    [JsonPropertyName("board")]
    public CloverBoard Board { get; private set; } = new();

    [JsonInclude]
    [JsonPropertyName("cursorColorIndex")]
    public int CursorColorIndex { get; private set; } = 0;

    [JsonInclude]
    [JsonPropertyName("isDisconnected")]
    public bool IsDisconnected { get; private set; }

    public void MarkDisconnected() => IsDisconnected = true;

    public Player(PlayerId id, string name, bool isAdmin = false)
    {
        Id = id;
        Name = RequireName(name);
        IsAdmin = isAdmin;
    }

    internal void SetCursorColorIndex(int colorIndex) => CursorColorIndex = colorIndex;

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