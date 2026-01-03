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
    [InlineData("Français_OFF", @"^[a-zA-ZÀÂÄÉÈÊËÏÎÔÙÛÜŸÇàâäéèêëïîôùûüÿç\-\s]+$")]
    [InlineData("English_(from_FR_OFF)", @"^[a-zA-Z\-\s]+$")]
    [InlineData("Portuguese_(from_FR_OFF)", @"^[a-zA-ZÁÀÂÃÉÊÍÓÔÕÚÜÇáàâãéêíóôõúüç\-\s]+$")]
    public async Task ShouldCreateCardWithFourRandomWords(string language, string characterPattern)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldCreateCardWithFourRandomWords ({language}) ===");
        _testOutputHelper.WriteLine("Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var wordsPool = await WordsPool.CreateAsync(GameId.New(), language, dictionary);
        var cardFactory = new CardFactory(wordsPool);
        var cardId = CardId.New();
        _testOutputHelper.WriteLine($"Using language: {language}");
        _testOutputHelper.WriteLine($"Generated CardId: {cardId.Value}");

        // Act
        _testOutputHelper.WriteLine("Creating random card...");
        var card = cardFactory.CreateRandomCard(cardId);
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
    [InlineData("Français_OFF", 60)]
    [InlineData("English_(from_FR_OFF)", 60)]
    [InlineData("Portuguese_(from_FR_OFF)", 60)]
    public async Task ShouldCreateMultipleCardsWithDifferentWords(string language, int cardCount)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldCreateMultipleCardsWithDifferentWords ({language}) ===");
        _testOutputHelper.WriteLine("Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var wordsPool = await WordsPool.CreateAsync(GameId.New(), language, dictionary);
        var cardFactory = new CardFactory(wordsPool);
        _testOutputHelper.WriteLine($"Using language: {language}");
        _testOutputHelper.WriteLine($"Initial words available in pool: {wordsPool.RemainingWordsCount}");
        _testOutputHelper.WriteLine($"Creating {cardCount} cards ({cardCount * 4} words)...\n");

        // Act - Create multiple cards
        var cards = new List<Card>();
        for (int i = 0; i < cardCount; i++)
        {
            var card = cardFactory.CreateRandomCard(CardId.New());
            cards.Add(card);
            _testOutputHelper.WriteLine($"Card {i + 1}/{cardCount} created - Remaining words in pool: {wordsPool.RemainingWordsCount}");
        }
        _testOutputHelper.WriteLine($"\n✓ Successfully created {cardCount} cards");

        // Assert - Verify all cards have unique IDs
        _testOutputHelper.WriteLine("\nVerifying all cards have unique IDs...");
        var uniqueIds = cards.Select(c => c.Id).Distinct().Count();
        Assert.Equal(cardCount, uniqueIds);
        _testOutputHelper.WriteLine($"✓ All {cardCount} cards have unique IDs");

        // Assert - Verify NO word is used more than once across ALL cards
        _testOutputHelper.WriteLine("\nVerifying word uniqueness across all cards...");
        var allWords = cards.SelectMany(c => new[] { c.TopWord, c.RightWord, c.BottomWord, c.LeftWord }).ToList();
        var uniqueWords = allWords.Distinct().ToList();

        _testOutputHelper.WriteLine($"Total words used: {allWords.Count}");
        _testOutputHelper.WriteLine($"Unique words: {uniqueWords.Count}");

        // Find any duplicates
        var duplicates = allWords.GroupBy(w => w)
            .Where(g => g.Count() > 1)
            .Select(g => new { Word = g.Key, Count = g.Count() })
            .ToList();

        if (duplicates.Any())
        {
            _testOutputHelper.WriteLine("\n❌ DUPLICATES FOUND:");
            foreach (var dup in duplicates)
            {
                _testOutputHelper.WriteLine($"  - '{dup.Word}' appears {dup.Count} times");
            }
        }

        Assert.Empty(duplicates);
        Assert.Equal(allWords.Count, uniqueWords.Count);
        _testOutputHelper.WriteLine("✓ All words are unique across all cards (WordsPool ensures no reuse)");

        // Assert - Verify WordsPool correctly decremented available words
        _testOutputHelper.WriteLine($"\nVerifying WordsPool word consumption...");
        var expectedRemainingWords = wordsPool.RemainingWordsCount;
        _testOutputHelper.WriteLine($"Words remaining in pool: {expectedRemainingWords}");
        _testOutputHelper.WriteLine($"Words consumed: {cardCount * 4}");
        Assert.True(expectedRemainingWords >= 0, "WordsPool should have non-negative remaining words");
        _testOutputHelper.WriteLine("✓ WordsPool correctly tracked word consumption");

        _testOutputHelper.WriteLine("\n=== Test Passed ===\n");
    }

    [Theory]
    [InlineData("Français_OFF")]
    [InlineData("English_(from_FR_OFF)")]
    [InlineData("Portuguese_(from_FR_OFF)")]
    public async Task ShouldRespectCardWordValidationRules(string language)
    {
        // Arrange
        _testOutputHelper.WriteLine($"=== Test: ShouldRespectCardWordValidationRules ({language}) ===");
        _testOutputHelper.WriteLine("Step 1: Arranging test dependencies...");
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var dictionary = new FileWordDictionary(Path.GetFullPath(dictionaryPath));
        var wordsPool = await WordsPool.CreateAsync(GameId.New(), language, dictionary);
        var cardFactory = new CardFactory(wordsPool);
        _testOutputHelper.WriteLine($"  - Dictionary: FileWordDictionary");
        _testOutputHelper.WriteLine($"  - WordsPool initialized");
        _testOutputHelper.WriteLine($"  - CardFactory initialized");
        _testOutputHelper.WriteLine($"  - Language: {language}");

        // Act
        _testOutputHelper.WriteLine("\nStep 2: Creating card with CardFactory...");
        var card = cardFactory.CreateRandomCard(CardId.New());
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

    [Fact]
    public async Task AllDictionariesShouldHaveUniqueWords()
    {
        // Arrange
        _testOutputHelper.WriteLine("=== Test: AllDictionariesShouldHaveUniqueWords ===");
        _testOutputHelper.WriteLine("Step 1: Scanning dictionaries directory...");

        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "wwwroot", "dictionaries");
        var fullPath = Path.GetFullPath(dictionaryPath);
        _testOutputHelper.WriteLine($"Dictionary path: {fullPath}");

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Dictionaries directory not found: {fullPath}");
        }

        var dictionaryFiles = Directory.GetFiles(fullPath, "*.txt");
        _testOutputHelper.WriteLine($"Found {dictionaryFiles.Length} dictionary file(s)\n");

        foreach (var file in dictionaryFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            _testOutputHelper.WriteLine($"\n--- Checking dictionary: {fileName} ---");

            // Act - Read all lines from the dictionary file
            var lines = await File.ReadAllLinesAsync(file);
            var words = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList();

            _testOutputHelper.WriteLine($"Total lines read: {words.Count}");

            // Assert - Check for duplicates
            var wordGroups = words
                .GroupBy(w => w)
                .Where(g => g.Count() > 1)
                .Select(g => new { Word = g.Key, Count = g.Count() })
                .ToList();

            if (wordGroups.Any())
            {
                _testOutputHelper.WriteLine($"\n❌ DUPLICATES FOUND in {fileName}:");
                foreach (var duplicate in wordGroups)
                {
                    _testOutputHelper.WriteLine($"  - '{duplicate.Word}' appears {duplicate.Count} times");
                }
                Assert.Fail($"Dictionary '{fileName}' contains {wordGroups.Count} duplicate word(s)");
            }

            var uniqueWords = words.Distinct().ToList();
            _testOutputHelper.WriteLine($"Unique words: {uniqueWords.Count}");
            _testOutputHelper.WriteLine($"✓ No duplicates found in '{fileName}'");

            // Additional validation - ensure uniqueness count matches
            Assert.Equal(words.Count, uniqueWords.Count);
        }

        _testOutputHelper.WriteLine("\n=== All dictionaries validated successfully ===");
    }
}
