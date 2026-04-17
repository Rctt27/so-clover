using SoClover.Domain;
using Xunit;

namespace SoClover.Tests.Domain.Validation;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Nature", "nature")]
    [InlineData("  NATURE  ", "nature")]
    [InlineData("Café", "cafe")]
    [InlineData("Élève", "eleve")]
    [InlineData("Œuf", "oeuf")]
    [InlineData("œuvre", "oeuvre")]
    [InlineData("Ægir", "aegir")]
    [InlineData("l’ami", "l'ami")]
    [InlineData("", "")]
    public void Normalize_returns_expected(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_null_returns_empty()
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(null));
    }
}