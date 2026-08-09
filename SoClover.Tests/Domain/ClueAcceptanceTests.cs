using SoClover.Domain;
using SoClover.Domain.Validation;
using Xunit;

namespace SoClover.Tests.Domain;

public class ClueAcceptanceTests
{
    private static CloverBoard BuildBoard()
    {
        var board = new CloverBoard();
        var words = new[]
        {
            new[] { "Chirurgien", "Route",  "Plage",    "Ciel" },
            new[] { "Enfant",     "Rocher", "Sable",    "Île" },
            new[] { "Oiseau",     "Forêt",  "Montagne", "Vent" },
            new[] { "Rivière",    "Pont",   "Ville",    "Nature" },
        };
        var positions = new[]
        {
            BoardPosition.TopLeft, BoardPosition.TopRight,
            BoardPosition.BottomRight, BoardPosition.BottomLeft,
        };
        for (var i = 0; i < 4; i++)
        {
            var card = new Card(CardId.New(), words[i][0], words[i][1], words[i][2], words[i][3]);
            board.Place(positions[i], new OrientedCard(card, Rotation.None));
        }
        return board;
    }

    [Fact]
    public void Accepts_a_clue_unrelated_to_every_board_word()
    {
        var result = ClueAcceptance.Check(
            "Hôpital", Direction.Top, BuildBoard(), new FrenchOffClueValidator());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Rejects_a_clue_longer_than_MaxClueLength_without_calling_the_validator()
    {
        var tooLong = new string('a', Game.MaxClueLength + 1);

        var result = ClueAcceptance.Check(
            tooLong, Direction.Top, BuildBoard(), new FrenchOffClueValidator());

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ClueValidationRule.TooLong, error.Rule);
        Assert.Equal(Game.MaxClueLength, error.MaxLength);
    }

    // Le plafond porte sur la valeur *trimée* : c'est le comportement de Game.SetClue.
    [Fact]
    public void Trims_before_applying_the_length_cap()
    {
        var padded = "   " + new string('a', Game.MaxClueLength) + "   ";

        var result = ClueAcceptance.Check(
            padded, Direction.Top, BuildBoard(), new FrenchOffClueValidator());

        Assert.True(result.IsValid);
    }

    // R1 — sous-chaîne bidirectionnelle contre un mot du board.
    [Fact]
    public void Rejects_a_clue_containing_a_board_word()
    {
        var result = ClueAcceptance.Check(
            "Chirurgienne", Direction.Top, BuildBoard(), new FrenchOffClueValidator());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Rule == ClueValidationRule.ExactMatch);
    }

    // R2 — radical FR : "Nature" perd sa voyelle finale, "Naturiste" contient "Natur".
    [Fact]
    public void Rejects_a_clue_sharing_a_French_stem_with_a_board_word()
    {
        var result = ClueAcceptance.Check(
            "Naturiste", Direction.Top, BuildBoard(), new FrenchOffClueValidator());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Rule == ClueValidationRule.SimilarStem);
    }

    [Fact]
    public void Throws_on_an_empty_clue_like_Game_SetClue_does()
    {
        Assert.Throws<InvalidClueException>(() =>
            ClueAcceptance.Check("   ", Direction.Top, BuildBoard(), new FrenchOffClueValidator()));
    }

    [Fact]
    public void Delegates_to_the_supplied_validator()
    {
        var result = ClueAcceptance.Check(
            "Chirurgien", Direction.Top, BuildBoard(), NullClueValidator.Instance);

        Assert.True(result.IsValid);
    }
}
