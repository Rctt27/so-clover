using SoClover.Domain.Validation;
using SoClover.Infrastructure.Validation;
using Xunit;

namespace SoClover.Tests.Domain.Validation;

public class SemanticValidationSupportTests
{
    [Theory]
    [InlineData("Français_OFF", "Français_OFF")]
    [InlineData("francais_off", "Français_OFF")]
    [InlineData("FRANÇAIS_OFF", "Français_OFF")]
    [InlineData("English_(from_FR_OFF)", "English_(from_FR_OFF)")]
    [InlineData("english", "English_(from_FR_OFF)")]
    [InlineData("Portuguese_(from_FR_OFF)", "Portuguese_(from_FR_OFF)")]
    [InlineData("portuguese", "Portuguese_(from_FR_OFF)")]
    // Diacritics are stripped by TextNormalizer, so the native spelling resolves through the same prefix.
    [InlineData("Português", "Portuguese_(from_FR_OFF)")]
    public void Find_returns_the_validator_of_the_language(string language, string expectedValidatorLanguage)
    {
        var validator = SemanticValidationSupport.Find(language);

        Assert.NotNull(validator);
        Assert.Equal(expectedValidatorLanguage, validator!.Language);
    }

    [Theory]
    [InlineData("Klingon")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_returns_null_for_unsupported_or_blank(string? language)
    {
        Assert.Null(SemanticValidationSupport.Find(language));
        if (language is not null)
            Assert.False(SemanticValidationSupport.IsSupported(language));
    }

    // The whole point of routing the factory through SemanticValidationSupport: the domain gate
    // (Game.SemanticClueCheckEnabled) and the factory can no longer disagree on the supported set.
    [Theory]
    [InlineData("Français_OFF")]
    [InlineData("English_(from_FR_OFF)")]
    [InlineData("Portuguese_(from_FR_OFF)")]
    [InlineData("Klingon")]
    public void Factory_returns_a_real_validator_exactly_when_the_language_is_supported(string language)
    {
        var validator = new ClueValidatorFactory().GetFor(language, semanticCheckEnabled: true);

        var isReal = !ReferenceEquals(validator, NullClueValidator.Instance);
        Assert.Equal(SemanticValidationSupport.IsSupported(language), isReal);
    }

    [Fact]
    public void Factory_returns_null_validator_when_semantic_check_is_disabled()
    {
        var validator = new ClueValidatorFactory().GetFor("Français_OFF", semanticCheckEnabled: false);

        Assert.Same(NullClueValidator.Instance, validator);
    }
}
