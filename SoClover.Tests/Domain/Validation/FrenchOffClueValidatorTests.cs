using SoClover.Domain;
using SoClover.Domain.Validation;
using Xunit;

namespace SoClover.Tests.Domain.Validation;

public class FrenchOffClueValidatorTests
{
    private readonly FrenchOffClueValidator _sut = new();

    private static CloverBoard BoardWithWords(params string[] words)
    {
        // Fills the 4 cards with 16 words (pad with "_" so each card has 4 non-empty words)
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
    public void R1_exact_equal_word_is_invalid()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("nature", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
        Assert.Equal("nature", result.Errors[0].CardWord);
    }

    [Fact]
    public void R1_clue_contains_card_word_is_invalid()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("naturellement", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_card_word_contains_clue_is_invalid()
    {
        var board = BoardWithWords("bondir");
        var result = _sut.Validate("bond", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_accent_insensitive()
    {
        var board = BoardWithWords("café");
        var result = _sut.Validate("Cafeine", Direction.Top, board);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void R1_case_insensitive()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("NATURE", Direction.Top, board);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void R1_ligature_insensitive()
    {
        var board = BoardWithWords("œuf");
        var result = _sut.Validate("oeufrier", Direction.Top, board);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void R1_short_card_word_is_skipped()
    {
        var board = BoardWithWords("si");
        var result = _sut.Validate("siège", Direction.Top, board);
        Assert.True(result.IsValid); // "si" < 3 chars → R1 skip; R2 also inapplicable
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
        var result = _sut.Validate("chien", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Language_is_french_off()
    {
        Assert.Equal("Français_OFF", _sut.Language);
    }
}