namespace SoClover.Domain;

public sealed class Card
{
    public CardId Id { get; }
    public string TopWord { get; }
    public string RightWord { get; }
    public string BottomWord { get; }
    public string LeftWord { get; }

    public Card(CardId id, string topWord, string rightWord, string bottomWord, string leftWord)
    {
        Id = id;
        TopWord = RequireWord(topWord);
        RightWord = RequireWord(rightWord);
        BottomWord = RequireWord(bottomWord);
        LeftWord = RequireWord(leftWord);
    }

    private static string RequireWord(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
            throw new DomainException("Word cannot be empty.");
        if (v.Length > 32)
            throw new DomainException("Word cannot exceed 32 characters.");
        return v;
    }
}