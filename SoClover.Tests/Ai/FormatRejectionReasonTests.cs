using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.Ai;

public class FormatRejectionReasonTests
{
    [Fact]
    public void French_provider_formats_TooLong_with_max_length()
    {
        var provider = new FrenchAiCluePromptProvider();
        var result = ClueValidationResult.Invalid(
            new ClueValidationError(ClueValidationRule.TooLong, string.Empty, null, 14));

        var reason = provider.FormatRejectionReason(result);

        Assert.Contains("trop long", reason);
        Assert.Contains("14", reason);
    }

    [Fact]
    public void English_provider_formats_TooLong_with_max_length()
    {
        var provider = new EnglishAiCluePromptProvider();
        var result = ClueValidationResult.Invalid(
            new ClueValidationError(ClueValidationRule.TooLong, string.Empty, null, 14));

        var reason = provider.FormatRejectionReason(result);

        Assert.Contains("too long", reason);
        Assert.Contains("14", reason);
    }
}
