namespace SoClover.Domain;

public sealed class CardFactory
{
    private readonly IWordDictionary _wordDictionary;

    public CardFactory(IWordDictionary wordDictionary)
    {
        _wordDictionary = wordDictionary;
    }

    public async Task<Card> CreateRandomCardAsync(CardId id, string language, CancellationToken ct = default)
    {
        var words = await _wordDictionary.GetRandomWordsAsync(language, 4, ct);

        if (words.Count != 4)
            throw new InvalidOperationException($"Expected 4 words but got {words.Count}");

        return new Card(id, words[0], words[1], words[2], words[3]);
    }
}
