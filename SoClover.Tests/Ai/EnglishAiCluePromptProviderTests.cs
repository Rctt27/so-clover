using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class EnglishAiCluePromptProviderTests
{
    private static BoardCluesPromptContext SampleContext(
        IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>>? rejected = null)
    {
        var cards = new[]
        {
            new BoardCardSnapshot(BoardPosition.TopLeft,
                TopWord: "moon",  RightWord: "road",   BottomWord: "beach",    LeftWord: "sky"),
            new BoardCardSnapshot(BoardPosition.TopRight,
                TopWord: "wave",  RightWord: "rock",   BottomWord: "sand",     LeftWord: "island"),
            new BoardCardSnapshot(BoardPosition.BottomRight,
                TopWord: "bird",  RightWord: "forest", BottomWord: "mountain", LeftWord: "wind"),
            new BoardCardSnapshot(BoardPosition.BottomLeft,
                TopWord: "river", RightWord: "bridge", BottomWord: "city",     LeftWord: "village"),
        };

        return new BoardCluesPromptContext(
            Language: "English_(from_FR_OFF)",
            Cards: cards,
            RemainingDirections: new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
            RejectedPerDirection: rejected
                ?? new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());
    }

    [Fact]
    public void Language_is_English()
    {
        Assert.Equal("English_(from_FR_OFF)", new EnglishAiCluePromptProvider().Language);
    }

    [Fact]
    public void BuildBoardCluesPrompt_uses_packaged_en_prompt_with_english_labels()
    {
        var bundle = new EnglishAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());

        Assert.Contains("So Clover", bundle.SystemPrompt);
        Assert.Contains("Card TopLeft", bundle.UserPrompt);
        Assert.Contains("Card TopRight", bundle.UserPrompt);
        Assert.Contains("find a clue word that evokes both", bundle.UserPrompt);
        Assert.Contains("moon", bundle.UserPrompt);
        Assert.DoesNotContain("Carte", bundle.UserPrompt);
        Assert.DoesNotContain("évoque à la fois", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
        Assert.DoesNotContain("}}", bundle.UserPrompt);
        Assert.NotNull(bundle.PromptVersion);
    }

    [Fact]
    public void BuildBoardCluesPrompt_retry_block_uses_english_rejected_wording()
    {
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[] { new RejectedAttempt("wave", "ExactMatch with 'beach'") },
        };

        var bundle = new EnglishAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext(rejected));

        Assert.Contains("Direction Top:", bundle.UserPrompt);
        Assert.Contains("rejected", bundle.UserPrompt);
        Assert.Contains("\"wave\"", bundle.UserPrompt);
        Assert.DoesNotContain("rejeté", bundle.UserPrompt);
    }

    [Fact]
    public void FormatRejectionReason_renders_english_wording()
    {
        var result = ClueValidationResult.Invalid(
            new ClueValidationError(ClueValidationRule.ExactMatch, "beach", Direction.Top));

        var reason = new EnglishAiCluePromptProvider().FormatRejectionReason(result);

        Assert.Contains("with the word \"beach\"", reason);
        Assert.Contains("ExactMatch", reason);
        Assert.DoesNotContain("avec le mot", reason);
    }
}
