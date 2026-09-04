using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class PortugueseAiCluePromptProviderTests
{
    private static BoardCluesPromptContext SampleContext(
        IReadOnlyList<Direction>? remaining = null,
        IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>>? rejected = null)
    {
        var cards = new[]
        {
            new BoardCardSnapshot(BoardPosition.TopLeft,
                TopWord: "lua",     RightWord: "estrada", BottomWord: "praia",    LeftWord: "céu"),
            new BoardCardSnapshot(BoardPosition.TopRight,
                TopWord: "onda",    RightWord: "rocha",   BottomWord: "areia",    LeftWord: "ilha"),
            new BoardCardSnapshot(BoardPosition.BottomRight,
                TopWord: "pássaro", RightWord: "floresta", BottomWord: "montanha", LeftWord: "vento"),
            new BoardCardSnapshot(BoardPosition.BottomLeft,
                TopWord: "rio",     RightWord: "ponte",   BottomWord: "cidade",   LeftWord: "vilarejo"),
        };

        return new BoardCluesPromptContext(
            Language: "Portuguese_(from_FR_OFF)",
            Cards: cards,
            RemainingDirections: remaining
                ?? new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
            RejectedPerDirection: rejected
                ?? new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());
    }

    [Fact]
    public void Language_is_Portuguese()
    {
        Assert.Equal("Portuguese_(from_FR_OFF)", new PortugueseAiCluePromptProvider().Language);
    }

    [Fact]
    public void BuildBoardCluesPrompt_uses_packaged_pt_prompt_with_portuguese_labels()
    {
        var bundle = new PortugueseAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());

        Assert.Contains("So Clover", bundle.SystemPrompt);
        Assert.Contains("Carta TopLeft", bundle.UserPrompt);
        Assert.Contains("Carta TopRight", bundle.UserPrompt);
        Assert.Contains("encontre uma palavra-pista que evoque", bundle.UserPrompt);
        Assert.Contains("lua", bundle.UserPrompt);
        // No leakage from the two languages this one was derived from.
        Assert.DoesNotContain("Carte ", bundle.UserPrompt);
        Assert.DoesNotContain("évoque à la fois", bundle.UserPrompt);
        Assert.DoesNotContain("find a clue word", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
        Assert.DoesNotContain("}}", bundle.UserPrompt);
        Assert.NotNull(bundle.PromptVersion);
    }

    [Fact]
    public void BuildSingleDirectionCluePrompt_uses_packaged_pt_per_direction_prompt()
    {
        var bundle = new PortugueseAiCluePromptProvider()
            .BuildSingleDirectionCluePrompt(SampleContext(remaining: new[] { Direction.Top }));

        Assert.Contains("So Clover", bundle.SystemPrompt);
        Assert.Contains("\"candidates\"", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
        Assert.DoesNotContain("}}", bundle.UserPrompt);
        // Only the requested direction is asked for.
        Assert.Contains("Top", bundle.UserPrompt);
    }

    [Fact]
    public void BuildSingleDirectionCluePrompt_with_reasoning_loads_the_packaged_pt_reasoning_variant()
    {
        var ctx = SampleContext(remaining: new[] { Direction.Top }) with { IncludeReasoning = true };

        var bundle = new PortugueseAiCluePromptProvider().BuildSingleDirectionCluePrompt(ctx);

        // The reasoning-only variant states it has a native thinking phase and drops the scratchpad.
        Assert.Contains("fase de reflexão nativa", bundle.SystemPrompt);
        Assert.DoesNotContain("\"candidates\"", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_retry_block_uses_portuguese_rejected_wording()
    {
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[] { new RejectedAttempt("onda", "ExactMatch com a palavra \"praia\"") },
        };

        var bundle = new PortugueseAiCluePromptProvider()
            .BuildBoardCluesPrompt(SampleContext(rejected: rejected));

        Assert.Contains("Direção Top:", bundle.UserPrompt);
        Assert.Contains("rejeitada", bundle.UserPrompt);
        Assert.Contains("\"onda\"", bundle.UserPrompt);
    }

    [Fact]
    public void FormatRejectionReason_is_written_in_portuguese()
    {
        var provider = new PortugueseAiCluePromptProvider();

        var withDirection = provider.FormatRejectionReason(ClueValidationResult.Invalid(new[]
        {
            new ClueValidationError(ClueValidationRule.ExactMatch, "praia", Direction.Top),
        }));
        var tooLong = provider.FormatRejectionReason(ClueValidationResult.Invalid(new[]
        {
            new ClueValidationError(ClueValidationRule.TooLong, string.Empty, null, MaxLength: 14),
        }));

        Assert.Equal("ExactMatch com a palavra \"praia\" (direção Top)", withDirection);
        Assert.Equal("pista longa demais (máx. 14 caracteres)", tooLong);
    }

    [Fact]
    public void SingleDirection_reasoning_on_with_missing_dedicated_file_throws_filenotfound()
    {
        var standardPath = Path.Combine(Path.GetTempPath(), $"test-std-{Guid.NewGuid()}.md");
        var missingReasoningPath = Path.Combine(Path.GetTempPath(), $"test-missing-{Guid.NewGuid()}.md");
        File.WriteAllText(standardPath, """
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

# RETRY_FEEDBACK
{{rejectedAttemptsByDirection}}
""");
        try
        {
            var provider = new PortugueseAiCluePromptProvider(
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
}
