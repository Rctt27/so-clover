namespace SoClover.Domain;

public sealed class CardFactory
{
    private readonly WordsPool _wordsPool;

    public CardFactory(WordsPool wordsPool)
    {
        _wordsPool = wordsPool;
    }

    public Card CreateRandomCard(CardId id)
    {
        var words = _wordsPool.DrawWords(4);

        if (words.Count != 4)
            throw new InvalidOperationException($"Expected 4 words but got {words.Count}");

        return new Card(id, words[0], words[1], words[2], words[3]);
    }
}
