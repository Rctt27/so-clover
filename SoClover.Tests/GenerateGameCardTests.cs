using SoClover.Domain;
using SoClover.Infrastructure;
using Xunit;

namespace SoClover.Tests;

public class GenerateGameCardTests
{
    private static FileWordDictionary BuildDictionary()
    {
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        return new FileWordDictionary(Path.GetFullPath(dictionaryPath));
    }

    [Theory]
    [InlineData("Français_OFF")]
    [InlineData("English_(from_FR_OFF)")]
    [InlineData("Portuguese_(from_FR_OFF)")]
    public async Task CreateRandomCard_produces_four_valid_words(string language)
    {
        var wordsPool = await WordsPool.CreateAsync(GameId.New(), language, BuildDictionary());
        var cardFactory = new CardFactory(wordsPool);
        var cardId = CardId.New();

        var card = cardFactory.CreateRandomCard(cardId);

        Assert.Equal(cardId, card.Id);
        foreach (var word in new[] { card.TopWord, card.RightWord, card.BottomWord, card.LeftWord })
        {
            Assert.False(string.IsNullOrWhiteSpace(word));
            Assert.True(word.Length <= 32);
        }
    }

    [Theory]
    [InlineData("Français_OFF")]
    [InlineData("English_(from_FR_OFF)")]
    [InlineData("Portuguese_(from_FR_OFF)")]
    public async Task CreateRandomCard_never_reuses_a_word_across_cards(string language)
    {
        const int cardCount = 60;
        var wordsPool = await WordsPool.CreateAsync(GameId.New(), language, BuildDictionary());
        var cardFactory = new CardFactory(wordsPool);

        var cards = Enumerable.Range(0, cardCount)
            .Select(_ => cardFactory.CreateRandomCard(CardId.New()))
            .ToList();

        Assert.Equal(cardCount, cards.Select(c => c.Id).Distinct().Count());

        var allWords = cards.SelectMany(c => new[] { c.TopWord, c.RightWord, c.BottomWord, c.LeftWord }).ToList();
        Assert.Equal(allWords.Count, allWords.Distinct().Count());
    }
}
