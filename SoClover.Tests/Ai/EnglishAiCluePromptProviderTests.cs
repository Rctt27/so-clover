using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class EnglishAiCluePromptProviderTests
{
    private static BoardCluesPromptContext SampleContext(
        IReadOnlyList<Direction>? remaining = null,
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
            RemainingDirections: remaining
                ?? new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
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

        var bundle = new EnglishAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext(rejected: rejected));

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

    [Fact]
    public void BuildBoardCluesPrompt_includes_reasoning_block_in_system_when_reasoning_enabled()
    {
        var bundle = new EnglishAiCluePromptProvider()
            .BuildBoardCluesPrompt(SampleContext() with { IncludeReasoning = true });

        Assert.Contains("mentally and compactly", bundle.SystemPrompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_omits_reasoning_block_when_reasoning_disabled_by_default()
    {
        var bundle = new EnglishAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());

        Assert.DoesNotContain("mentally and compactly", bundle.SystemPrompt);
    }

    private static void WithDualFixture(
        string standardBody, string reasoningBody, Action<EnglishAiCluePromptProvider> body)
    {
        var standardPath = Path.Combine(Path.GetTempPath(), $"test-std-{Guid.NewGuid()}.md");
        var reasoningPath = Path.Combine(Path.GetTempPath(), $"test-rsn-{Guid.NewGuid()}.md");
        File.WriteAllText(standardPath, standardBody);
        File.WriteAllText(reasoningPath, reasoningBody);
        try
        {
            var provider = new EnglishAiCluePromptProvider(
                new FilePromptLoader(), standardPath, standardPath, reasoningPath);
            body(provider);
        }
        finally
        {
            if (File.Exists(standardPath)) File.Delete(standardPath);
            if (File.Exists(reasoningPath)) File.Delete(reasoningPath);
        }
    }

    private static void WithFixture(string template, Action<EnglishAiCluePromptProvider> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.md");
        File.WriteAllText(path, template);
        try
        {
            var provider = new EnglishAiCluePromptProvider(new FilePromptLoader(), path, path);
            body(provider);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private const string StandardSingleTemplate = """
---
version: 1
---
# SYSTEM
SYSTEM-STANDARD-SENTINEL

# USER
{{boardLayout}}
{{directionToResolve}}
{{allBoardWordsList}}
{{retryFeedback}}

# REASONING
REASONING-APPENDED-SENTINEL

# RETRY_FEEDBACK
{{rejectedAttemptsByDirection}}
""";

    private const string ReasoningSingleTemplate = """
---
version: 7
---
# SYSTEM
SYSTEM-REASONING-SENTINEL

# USER
{{boardLayout}}
{{directionToResolve}}
{{allBoardWordsList}}
{{retryFeedback}}

# REASONING
IGNORED-REASONING-SECTION-SENTINEL

# RETRY_FEEDBACK
{{rejectedAttemptsByDirection}}
""";

    [Fact]
    public void SingleDirection_reasoning_on_with_dedicated_file_uses_reasoning_system_and_version()
    {
        WithDualFixture(StandardSingleTemplate, ReasoningSingleTemplate, provider =>
        {
            var ctx = SampleContext(remaining: new[] { Direction.Top }) with { IncludeReasoning = true };
            var bundle = provider.BuildSingleDirectionCluePrompt(ctx);

            Assert.Contains("SYSTEM-REASONING-SENTINEL", bundle.SystemPrompt);
            Assert.DoesNotContain("SYSTEM-STANDARD-SENTINEL", bundle.SystemPrompt);
            Assert.DoesNotContain("IGNORED-REASONING-SECTION-SENTINEL", bundle.SystemPrompt);
            Assert.Equal(7, bundle.PromptVersion);
        });
    }

    [Fact]
    public void SingleDirection_reasoning_off_with_dedicated_file_uses_standard_system_and_version()
    {
        WithDualFixture(StandardSingleTemplate, ReasoningSingleTemplate, provider =>
        {
            var ctx = SampleContext(remaining: new[] { Direction.Top });
            var bundle = provider.BuildSingleDirectionCluePrompt(ctx);

            Assert.Contains("SYSTEM-STANDARD-SENTINEL", bundle.SystemPrompt);
            Assert.DoesNotContain("SYSTEM-REASONING-SENTINEL", bundle.SystemPrompt);
            Assert.Equal(1, bundle.PromptVersion);
        });
    }

    [Fact]
    public void SingleDirection_reasoning_on_with_missing_dedicated_file_throws_filenotfound()
    {
        var standardPath = Path.Combine(Path.GetTempPath(), $"test-std-{Guid.NewGuid()}.md");
        var missingReasoningPath = Path.Combine(Path.GetTempPath(), $"test-missing-{Guid.NewGuid()}.md");
        File.WriteAllText(standardPath, StandardSingleTemplate);
        try
        {
            var provider = new EnglishAiCluePromptProvider(
                new FilePromptLoader(), standardPath, standardPath, missingReasoningPath);
            var ctx = SampleContext(remaining: new[] { Direction.Top }) with { IncludeReasoning = true };

            var ex = Assert.Throws<FileNotFoundException>(
                () => provider.BuildSingleDirectionCluePrompt(ctx));
            Assert.Contains(missingReasoningPath, ex.Message + ex.FileName);
        }
        finally
        {
            if (File.Exists(standardPath)) File.Delete(standardPath);
        }
    }

    [Fact]
    public void SingleDirection_reasoning_on_with_null_path_falls_back_to_standard_and_appends_reasoning()
    {
        WithFixture(StandardSingleTemplate, provider =>
        {
            var ctx = SampleContext(remaining: new[] { Direction.Top }) with { IncludeReasoning = true };
            var bundle = provider.BuildSingleDirectionCluePrompt(ctx);

            Assert.Contains("SYSTEM-STANDARD-SENTINEL", bundle.SystemPrompt);
            Assert.Contains("REASONING-APPENDED-SENTINEL", bundle.SystemPrompt);
        });
    }
}
