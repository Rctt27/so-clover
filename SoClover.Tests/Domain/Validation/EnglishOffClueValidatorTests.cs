using SoClover.Domain;
using SoClover.Domain.Validation;
using Xunit;

namespace SoClover.Tests.Domain.Validation;

public class EnglishOffClueValidatorTests
{
    private readonly EnglishOffClueValidator _sut = new();

    private static CloverBoard BoardWithWords(params string[] words)
    {
        var padded = words.Concat(Enumerable.Repeat("PAD", 16 - words.Length)).Take(16).ToArray();
        var board = new CloverBoard();
        var cards = new[]
        {
            new Card(CardId.New(), padded[0], padded[1], padded[2], padded[3]),
            new Card(CardId.New(), padded[4], padded[5], padded[6], padded[7]),
            new Card(CardId.New(), padded[8], padded[9], padded[10], padded[11]),
            new Card(CardId.New(), padded[12], padded[13], padded[14], padded[15])
        };
        board.Place(BoardPosition.TopLeft, new OrientedCard(cards[0], Rotation.None));
        board.Place(BoardPosition.TopRight, new OrientedCard(cards[1], Rotation.None));
        board.Place(BoardPosition.BottomRight, new OrientedCard(cards[2], Rotation.None));
        board.Place(BoardPosition.BottomLeft, new OrientedCard(cards[3], Rotation.None));
        return board;
    }

    [Fact]
    public void Language_is_english()
    {
        Assert.Equal("English_(from_FR_OFF)", _sut.Language);
    }

    [Fact]
    public void R1_exact_equal_word_is_invalid()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("nature", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_clue_contains_card_word_is_invalid()
    {
        var board = BoardWithWords("cat");
        var result = _sut.Validate("cats", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_card_word_contains_clue_is_invalid()
    {
        var board = BoardWithWords("table");
        var result = _sut.Validate("tabl", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_case_insensitive()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("NATURE", Direction.Top, board);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_clue_is_valid()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unrelated_clue_is_valid()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("dog", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_vowel_stem_is_not_applied_no_false_positive()
    {
        // "core" ends with vowel → French R2 would strip to "cor" and reject "corner".
        // English validator applies R1 only, so "corner" (unrelated to "core") is valid.
        var board = BoardWithWords("core");
        var result = _sut.Validate("corner", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_vowel_stem_is_not_applied_naturiste_case()
    {
        // French R2 would reject "naturist" against "nature" (stem "natur"). English keeps it valid.
        var board = BoardWithWords("nature");
        var result = _sut.Validate("naturist", Direction.Top, board);
        Assert.True(result.IsValid);
    }
}
