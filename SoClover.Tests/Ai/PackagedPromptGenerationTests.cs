using SoClover.Domain;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

/// <summary>
/// Cross-language guard on the packaged prompt files served from <c>bin/</c>.
///
/// The <c>version:</c> frontmatter denotes a <b>content generation shared across languages</b>, not
/// a per-file history: that is what makes the <c>PromptVersion</c> field of the "AI clue LLM call
/// completed" log comparable between FR, EN and PT. Editing a prompt's content without bumping its
/// version silently makes two different prompts report the same identity, and every measurement
/// taken across that boundary becomes incomparable. These tests pin the current generation of each
/// family so such an edit shows up as a red test rather than as a mystery in the eval ledger.
/// </summary>
public sealed class PackagedPromptGenerationTests
{
    public static TheoryData<string> Languages() => new()
    {
        "Français_OFF",
        "English_(from_FR_OFF)",
        "Portuguese_(from_FR_OFF)",
    };

    private static BoardCluesPromptContext SampleContext(string language, IReadOnlyList<Direction> remaining)
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
            Language: language,
            Cards: cards,
            RemainingDirections: remaining,
            RejectedPerDirection: new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());
    }

    private static IAiCluePromptProvider ProviderFor(string language)
    {
        var provider = new AiCluePromptProviderFactory().GetFor(language);
        Assert.NotNull(provider);
        return provider!;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Packaged_board_clues_is_generation_10(string language)
    {
        var bundle = ProviderFor(language).BuildBoardCluesPrompt(
            SampleContext(language, BoardGeometry.AllDirections.ToArray()));

        Assert.Equal(10, bundle.PromptVersion);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Packaged_per_direction_is_generation_5(string language)
    {
        var bundle = ProviderFor(language).BuildSingleDirectionCluePrompt(
            SampleContext(language, new[] { Direction.Top }));

        Assert.Equal(5, bundle.PromptVersion);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Packaged_per_direction_reasoning_variant_is_generation_1(string language)
    {
        var ctx = SampleContext(language, new[] { Direction.Top }) with { IncludeReasoning = true };

        var bundle = ProviderFor(language).BuildSingleDirectionCluePrompt(ctx);

        Assert.Equal(1, bundle.PromptVersion);
    }

    /// <summary>
    /// Generation 5 of the PerDirection family is defined by the annotated <c>candidates</c>
    /// scratchpad: a file claiming version 5 without asking for it is not that generation.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void Generation_5_per_direction_asks_for_the_annotated_candidates_scratchpad(string language)
    {
        var bundle = ProviderFor(language).BuildSingleDirectionCluePrompt(
            SampleContext(language, new[] { Direction.Top }));

        Assert.Contains("\"candidates\"", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
        Assert.DoesNotContain("}}", bundle.UserPrompt);
    }

    /// <summary>
    /// Every language with a dedicated <c>.reasoning.md</c> path must NOT also carry a
    /// <c># REASONING</c> section in its standard PerDirection file: the dedicated file replaces it
    /// wholesale, so an inline section would be dead code. Asserted through behaviour — the reasoning
    /// bundle must differ from the standard one and be the short reasoning-only variant.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void Reasoning_variant_replaces_the_standard_per_direction_prompt(string language)
    {
        var provider = ProviderFor(language);
        var standard = provider.BuildSingleDirectionCluePrompt(
            SampleContext(language, new[] { Direction.Top }));
        var reasoning = provider.BuildSingleDirectionCluePrompt(
            SampleContext(language, new[] { Direction.Top }) with { IncludeReasoning = true });

        Assert.NotEqual(standard.SystemPrompt, reasoning.SystemPrompt);
        // The reasoning variant drops the step-by-step procedure entirely.
        Assert.DoesNotContain("\"candidates\"", reasoning.UserPrompt);
        Assert.True(reasoning.UserPrompt.Length < standard.UserPrompt.Length);
    }
}
