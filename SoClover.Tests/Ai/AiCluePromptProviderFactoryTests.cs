using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class AiCluePromptProviderFactoryTests
{
    [Theory]
    [InlineData("Français_OFF")]
    [InlineData("francais_off")]
    [InlineData("Francais OFF")]
    [InlineData("FRANÇAIS_OFF")]
    public void IsLanguageSupported_returns_true_for_french_variants(string lang)
    {
        var factory = new AiCluePromptProviderFactory();
        Assert.True(factory.IsLanguageSupported(lang));
    }

    [Theory]
    [InlineData("Klingon")]
    [InlineData("English")]
    [InlineData("")]
    [InlineData(null)]
    public void IsLanguageSupported_returns_false_for_unsupported_or_null(string? lang)
    {
        var factory = new AiCluePromptProviderFactory();
        Assert.False(factory.IsLanguageSupported(lang!));
    }

    [Fact]
    public void GetFor_returns_french_provider_for_french_language()
    {
        var factory = new AiCluePromptProviderFactory();

        var provider = factory.GetFor("Français_OFF");

        Assert.NotNull(provider);
        Assert.Equal("Français_OFF", provider.Language);
        Assert.IsType<FrenchAiCluePromptProvider>(provider);
    }

    [Fact]
    public void GetFor_returns_null_for_unsupported_language()
    {
        var factory = new AiCluePromptProviderFactory();

        var provider = factory.GetFor("Klingon");

        Assert.Null(provider);
    }
}
