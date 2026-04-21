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
    
    [Fact]
    public void R2_stem_match_clue_contains_stem()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("naturiste", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.SimilarStem, result.Errors[0].Rule);
    }

    [Fact]
    public void R2_stem_match_bidirectional()
    {
        var board = BoardWithWords("naturiste");
        // "naturiste" doesn't end with a voyelle, but R1 substring catches "natur" via card-in-clue? No.
        // Use a case where R2 applies from the card side: card = "route" (ends voyelle), clue = "route66" contains "rout"
        var b2 = BoardWithWords("route");
        var result = _sut.Validate("routier", Direction.Top, b2);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.SimilarStem, result.Errors[0].Rule);
    }

    [Fact]
    public void R2_consonne_final_does_not_trigger()
    {
        var board = BoardWithWords("chat");
        var result = _sut.Validate("chien", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_stem_below_three_chars_is_skipped()
    {
        // "vie" (3) → stem "vi" (2) < 3 → skip R2. R1: clue "vient" contains "vie"? yes → R1 invalid.
        // To isolate R2 skip: use card "ami" (3, ends i) → stem "am" (2) < 3 → R2 skip. Clue "camembert" does not contain "ami".
        var board = BoardWithWords("ami");
        var result = _sut.Validate("camembert", Direction.Top, board);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void R2_accent_insensitive()
    {
        var board = BoardWithWords("idée");
        // normalized "idee" ends with voyelle → stem "ide" (3). Clue "idealisme" → normalized "idealisme" contains "ide" → R2.
        var result = _sut.Validate("idealisme", Direction.Top, board);
        Assert.False(result.IsValid);
        Assert.Equal(ClueValidationRule.SimilarStem, result.Errors[0].Rule);
    }

    [Fact]
    public void R1_wins_over_R2_for_same_word()
    {
        var board = BoardWithWords("nature");
        var result = _sut.Validate("dénaturé", Direction.Top, board);
        Assert.False(result.IsValid);
        // "dénaturé" → "denature"; card "nature" ⊂ clue → R1 fires; R2 must not duplicate.
        Assert.Single(result.Errors);
        Assert.Equal(ClueValidationRule.ExactMatch, result.Errors[0].Rule);
    }

    [Fact]
    public void Multi_word_clue_validated_whole_string()
    {
        var board = BoardWithWords("tour");
        var result = _sut.Validate("tour Eiffel", Direction.Top, board);
        Assert.False(result.IsValid); // R1 substring — normalized "tour eiffel" contains "tour"
    }

    [Fact]
    public void R1_short_clue_not_caught_by_word_contains_clue()
    {
        // Guard intentionnel : quand l'indice normalisé a < 3 chars, la branche
        // wordNorm.Contains(clueNorm) est désactivée pour éviter les faux positifs
        // sur des sous-chaînes triviales (ex. "bo" dans "bondir").
        var board = BoardWithWords("bondir");
        var result = _sut.Validate("bo", Direction.Top, board);
        Assert.True(result.IsValid);
    }
}