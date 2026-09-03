using SoClover.Domain;
using SoClover.Domain.Validation;
using Xunit;

namespace SoClover.Tests.Domain.Validation;

public class PortugueseOffClueValidatorTests
{
    private readonly PortugueseOffClueValidator _sut = new();

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
    public void Language_is_portuguese()
    {
        Assert.Equal("Portuguese_(from_FR_OFF)", _sut.Language);
    }

    [Fact]
    public void R1_exact_equal_word_is_invalid()
    {
        var board = BoardWithWords("mesa");
        var result = _sut.Validate("mesa", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_clue_contains_card_word_is_invalid()
    {
        var board = BoardWithWords("gato");
        var result = _sut.Validate("gatos", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_card_word_contains_clue_is_invalid()
    {
        var board = BoardWithWords("mesada");
        var result = _sut.Validate("mesa", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_is_diacritic_insensitive()
    {
        // "Avô" and "avo" normalize to the same string — the clue must still be rejected.
        var board = BoardWithWords("Avô");
        var result = _sut.Validate("AVO", Direction.Top, board);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_clue_is_valid()
    {
        var board = BoardWithWords("mesa");
        var result = _sut.Validate("", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unrelated_clue_is_valid()
    {
        var board = BoardWithWords("mesa");
        var result = _sut.Validate("cachorro", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_vowel_stem_is_not_applied_no_false_positive()
    {
        // Portuguese nouns overwhelmingly end in a vowel. The French R2 heuristic would strip
        // "mesa" to "mes" and wrongly reject "mesmo", which shares no root with it.
        var board = BoardWithWords("mesa");
        var result = _sut.Validate("mesmo", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_vowel_stem_is_not_applied_casa_case()
    {
        // "casa" → French R2 stem "cas" ⊂ "cascata": a false positive we deliberately do not raise.
        var board = BoardWithWords("casa");
        var result = _sut.Validate("cascata", Direction.Top, board);
        Assert.True(result.IsValid);
    }
}
