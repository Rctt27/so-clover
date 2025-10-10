using SoClover.Domain;
using SoClover.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SoClover.Tests;

public class GenerateGameCard
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GenerateGameCard(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("Français", @"^[A-ZÀÂÄÉÈÊËÏÎÔÙÛÜŸÇ\-]+$")]
    [InlineData("English", @"^[A-Z\-]+$")]
    public async Task ShouldCreateCardWithFourRandomWords(string language, string characterPattern)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldCreateCardWithFourRandomWords ({language}) ===");
        _testOutputHelper.WriteLine("Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var cardFactory = new CardFactory(dictionary);
        var cardId = CardId.New();
        _testOutputHelper.WriteLine($"Using language: {language}");
        _testOutputHelper.WriteLine($"Generated CardId: {cardId.Value}");

        // Act
        _testOutputHelper.WriteLine("Creating random card...");
        var card = await cardFactory.CreateRandomCardAsync(cardId, language);
        _testOutputHelper.WriteLine("Card created successfully!");

        // Assert
        Assert.NotNull(card);
        Assert.Equal(cardId, card.Id);

        // Log the generated card
        _testOutputHelper.WriteLine("\n--- Generated Card ---");
        _testOutputHelper.WriteLine($"Card ID: {card.Id.Value}");
        _testOutputHelper.WriteLine($"Position: TOP    | Word: {card.TopWord}");
        _testOutputHelper.WriteLine($"Position: RIGHT  | Word: {card.RightWord}");
        _testOutputHelper.WriteLine($"Position: BOTTOM | Word: {card.BottomWord}");
        _testOutputHelper.WriteLine($"Position: LEFT   | Word: {card.LeftWord}");
        _testOutputHelper.WriteLine("----------------------\n");

        // Verify all four words are non-empty and valid
        _testOutputHelper.WriteLine("Verifying words are non-empty...");
        Assert.False(string.IsNullOrWhiteSpace(card.TopWord));
        Assert.False(string.IsNullOrWhiteSpace(card.RightWord));
        Assert.False(string.IsNullOrWhiteSpace(card.BottomWord));
        Assert.False(string.IsNullOrWhiteSpace(card.LeftWord));
        _testOutputHelper.WriteLine("✓ All words are non-empty");

        // Verify word length constraints (from Card.cs)
        _testOutputHelper.WriteLine("Verifying word length constraints (max 32 characters)...");
        Assert.True(card.TopWord.Length <= 32);
        Assert.True(card.RightWord.Length <= 32);
        Assert.True(card.BottomWord.Length <= 32);
        Assert.True(card.LeftWord.Length <= 32);
        _testOutputHelper.WriteLine("✓ All words respect length constraints");

        // Verify words come from dictionary (basic check)
        _testOutputHelper.WriteLine($"Verifying words match {language} character pattern...");
        var allWords = new[] { card.TopWord, card.RightWord, card.BottomWord, card.LeftWord };
        Assert.All(allWords, word => Assert.Matches(characterPattern, word));
        _testOutputHelper.WriteLine($"✓ All words match {language} character pattern");
        _testOutputHelper.WriteLine("=== Test Passed ===\n");
    }

    [Theory]
    [InlineData("Français")]
    [InlineData("English")]
    public async Task ShouldCreateMultipleCardsWithDifferentWords(string language)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldCreateMultipleCardsWithDifferentWords ({language}) ===");
        _testOutputHelper.WriteLine("Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var cardFactory = new CardFactory(dictionary);
        _testOutputHelper.WriteLine($"Using language: {language}");

        // Act
        _testOutputHelper.WriteLine("Creating first card...");
        var card1 = await cardFactory.CreateRandomCardAsync(CardId.New(), language);
        _testOutputHelper.WriteLine("First card created successfully!");

        _testOutputHelper.WriteLine("Creating second card...");
        var card2 = await cardFactory.CreateRandomCardAsync(CardId.New(), language);
        _testOutputHelper.WriteLine("Second card created successfully!");

        // Log both cards
        _testOutputHelper.WriteLine("\n--- Card 1 ---");
        _testOutputHelper.WriteLine($"Card ID: {card1.Id.Value}");
        _testOutputHelper.WriteLine($"Position: TOP    | Word: {card1.TopWord}");
        _testOutputHelper.WriteLine($"Position: RIGHT  | Word: {card1.RightWord}");
        _testOutputHelper.WriteLine($"Position: BOTTOM | Word: {card1.BottomWord}");
        _testOutputHelper.WriteLine($"Position: LEFT   | Word: {card1.LeftWord}");

        _testOutputHelper.WriteLine("\n--- Card 2 ---");
        _testOutputHelper.WriteLine($"Card ID: {card2.Id.Value}");
        _testOutputHelper.WriteLine($"Position: TOP    | Word: {card2.TopWord}");
        _testOutputHelper.WriteLine($"Position: RIGHT  | Word: {card2.RightWord}");
        _testOutputHelper.WriteLine($"Position: BOTTOM | Word: {card2.BottomWord}");
        _testOutputHelper.WriteLine($"Position: LEFT   | Word: {card2.LeftWord}");
        _testOutputHelper.WriteLine("--------------\n");

        // Assert - Cards should have different IDs
        _testOutputHelper.WriteLine("Verifying cards have different IDs...");
        Assert.NotEqual(card1.Id, card2.Id);
        _testOutputHelper.WriteLine("✓ Cards have different IDs");

        // At least one word should be different (very high probability with random selection)
        _testOutputHelper.WriteLine("Verifying cards have different words...");
        var allWords1 = new[] { card1.TopWord, card1.RightWord, card1.BottomWord, card1.LeftWord };
        var allWords2 = new[] { card2.TopWord, card2.RightWord, card2.BottomWord, card2.LeftWord };
        var allSame = allWords1.SequenceEqual(allWords2);
        Assert.False(allSame, "Two randomly generated cards should very likely have at least one different word");
        _testOutputHelper.WriteLine("✓ Cards have at least one different word");
        _testOutputHelper.WriteLine("=== Test Passed ===\n");
    }

    [Theory]
    [InlineData("Français")]
    [InlineData("English")]
    public async Task ShouldRespectCardWordValidationRules(string language)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldRespectCardWordValidationRules ({language}) ===");
        _testOutputHelper.WriteLine("Step 1: Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var cardFactory = new CardFactory(dictionary);
        _testOutputHelper.WriteLine($"  - Dictionary: FileWordDictionary");
        _testOutputHelper.WriteLine($"  - CardFactory initialized");
        _testOutputHelper.WriteLine($"  - Language: {language}");

        // Act
        _testOutputHelper.WriteLine("\nStep 2: Creating card with CardFactory...");
        var card = await cardFactory.CreateRandomCardAsync(CardId.New(), language);
        _testOutputHelper.WriteLine("  - Card created successfully via factory");
        _testOutputHelper.WriteLine($"  - Card ID: {card.Id.Value}");
        _testOutputHelper.WriteLine($"  - Words generated: TOP={card.TopWord}, RIGHT={card.RightWord}, BOTTOM={card.BottomWord}, LEFT={card.LeftWord}");

        // Assert - Card constructor validates words, so if we get here, validation passed
        _testOutputHelper.WriteLine("\nStep 3: Validating card is not null...");
        Assert.NotNull(card);
        _testOutputHelper.WriteLine("  ✓ Card is not null");

        // Double-check that Card validation would accept these words
        _testOutputHelper.WriteLine("\nStep 4: Testing Card constructor validation by creating a new Card with same words...");
        _testOutputHelper.WriteLine($"  - Creating new Card with words: TOP={card.TopWord}, RIGHT={card.RightWord}, BOTTOM={card.BottomWord}, LEFT={card.LeftWord}");
        var testCard = new Card(
            CardId.New(),
            card.TopWord,
            card.RightWord,
            card.BottomWord,
            card.LeftWord
        );
        _testOutputHelper.WriteLine("  - New Card created successfully (validation passed)");
        _testOutputHelper.WriteLine($"  - Test Card ID: {testCard.Id.Value}");

        _testOutputHelper.WriteLine("\nStep 5: Verifying test card is not null...");
        Assert.NotNull(testCard);
        _testOutputHelper.WriteLine("  ✓ Test card is not null");

        _testOutputHelper.WriteLine("\nStep 6: Validation complete!");
        _testOutputHelper.WriteLine("  ✓ All words from CardFactory pass Card constructor validation");
        _testOutputHelper.WriteLine("  ✓ Words are non-empty (length > 0)");
        _testOutputHelper.WriteLine("  ✓ Words are within max length (≤ 32 characters)");
        _testOutputHelper.WriteLine("=== Test Passed ===\n");
    }
}
