using SoClover.Domain;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class FrenchAiCluePromptProviderTests
{
    private static BoardCluesPromptContext SampleContext(
        IReadOnlyList<Direction>? remaining = null,
        IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>>? rejected = null)
    {
        var cards = new[]
        {
            new BoardCardSnapshot(BoardPosition.TopLeft,
                TopWord: "lune",    RightWord: "route",  BottomWord: "plage",   LeftWord: "ciel"),
            new BoardCardSnapshot(BoardPosition.TopRight,
                TopWord: "vague",   RightWord: "rocher", BottomWord: "sable",   LeftWord: "île"),
            new BoardCardSnapshot(BoardPosition.BottomRight,
                TopWord: "oiseau",  RightWord: "forêt",  BottomWord: "montagne", LeftWord: "vent"),
            new BoardCardSnapshot(BoardPosition.BottomLeft,
                TopWord: "rivière", RightWord: "pont",   BottomWord: "ville",   LeftWord: "village"),
        };

        return new BoardCluesPromptContext(
            Language: "Français_OFF",
            Cards: cards,
            RemainingDirections: remaining
                ?? new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
            RejectedPerDirection: rejected
                ?? new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());
    }

    private static void WithFixture(string template, Action<FrenchAiCluePromptProvider> body)
    {
        var fixturePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.md");
        File.WriteAllText(fixturePath, template);
        try
        {
            var provider = new FrenchAiCluePromptProvider(new FilePromptLoader(), fixturePath, fixturePath);
            body(provider);
        }
        finally
        {
            if (File.Exists(fixturePath))
                File.Delete(fixturePath);
        }
    }

    private const string MinimalTemplate = """
# SYSTEM
Tu es un joueur expert.

# USER
{{boardLayout}}

À résoudre dans cet appel :
{{directionsToResolve}}

Tous les mots du board :
{{allBoardWordsList}}

{{retryFeedback}}

JSON.

# RETRY_FEEDBACK
Tes tentatives précédentes ont été rejetées :

{{rejectedAttemptsByDirection}}

Pour CHAQUE direction listée, propose un mot DIFFÉRENT.
""";

    [Fact]
    public void BuildBoardCluesPrompt_happy_path_lists_all_4_directions_and_omits_retry_feedback()
    {
        WithFixture(MinimalTemplate, provider =>
        {
            var bundle = provider.BuildBoardCluesPrompt(SampleContext());

            Assert.False(string.IsNullOrWhiteSpace(bundle.SystemPrompt));
            Assert.False(string.IsNullOrWhiteSpace(bundle.UserPrompt));

            Assert.Contains("Carte TopLeft", bundle.UserPrompt);
            Assert.Contains("Carte TopRight", bundle.UserPrompt);
            Assert.Contains("Carte BottomRight", bundle.UserPrompt);
            Assert.Contains("Carte BottomLeft", bundle.UserPrompt);

            Assert.Contains("lune", bundle.UserPrompt);
            Assert.Contains("ciel", bundle.UserPrompt);
            Assert.Contains("plage", bundle.UserPrompt);

            Assert.Contains("Top", bundle.UserPrompt);
            Assert.Contains("Right", bundle.UserPrompt);
            Assert.Contains("Bottom", bundle.UserPrompt);
            Assert.Contains("Left", bundle.UserPrompt);
            Assert.Contains("sable", bundle.UserPrompt);

            Assert.DoesNotContain("rejeté", bundle.UserPrompt, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("{{", bundle.UserPrompt);
            Assert.DoesNotContain("}}", bundle.UserPrompt);
        });
    }

    [Fact]
    public void BuildBoardCluesPrompt_emits_json_schema_with_clue_bounds()
    {
        WithFixture(MinimalTemplate, provider =>
        {
            var bundle = provider.BuildBoardCluesPrompt(SampleContext());

            Assert.Contains("\"clues\"", bundle.JsonSchema);
            Assert.Contains("\"minItems\": 1", bundle.JsonSchema);
            Assert.Contains("\"maxItems\": 4", bundle.JsonSchema);
        });
    }

    [Fact]
    public void Language_is_Francais_OFF()
    {
        WithFixture(MinimalTemplate, provider =>
        {
            Assert.Equal("Français_OFF", provider.Language);
        });
    }

    [Fact]
    public void BuildBoardCluesPrompt_uses_packaged_prompt_file_from_bin()
    {
        var provider = new FrenchAiCluePromptProvider();

        var bundle = provider.BuildBoardCluesPrompt(SampleContext());

        Assert.Contains("So Clover", bundle.SystemPrompt);
        Assert.Contains("Carte TopLeft", bundle.UserPrompt);
        Assert.Contains("Carte TopRight", bundle.UserPrompt);
        Assert.Contains("plage", bundle.UserPrompt);
        Assert.DoesNotContain("{{", bundle.UserPrompt);
        Assert.DoesNotContain("}}", bundle.UserPrompt);
        Assert.Contains("\"clues\"", bundle.JsonSchema);
    }

    [Fact]
    public void BuildBoardCluesPrompt_with_one_rejection_includes_feedback_for_that_direction()
    {
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[] { new RejectedAttempt("vague", "ExactMatch with 'plage'") },
        };

        var provider = new FrenchAiCluePromptProvider();
        var bundle = provider.BuildBoardCluesPrompt(SampleContext(rejected: rejected));

        Assert.Contains("Tes tentatives précédentes ont été rejetées", bundle.UserPrompt);
        Assert.Contains("Direction Top", bundle.UserPrompt);
        Assert.Contains("\"vague\"", bundle.UserPrompt);
        Assert.Contains("ExactMatch with 'plage'", bundle.UserPrompt);
        Assert.DoesNotContain("Direction Right", bundle.UserPrompt);
        Assert.DoesNotContain("{{rejectedAttemptsByDirection}}", bundle.UserPrompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_caps_feedback_at_three_per_direction_most_recent_first()
    {
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[]
            {
                new RejectedAttempt("a1", "r1"),
                new RejectedAttempt("a2", "r2"),
                new RejectedAttempt("a3", "r3"),
                new RejectedAttempt("a4", "r4"),
                new RejectedAttempt("a5", "r5"),
            },
        };

        var provider = new FrenchAiCluePromptProvider();
        var bundle = provider.BuildBoardCluesPrompt(SampleContext(rejected: rejected));
        var prompt = bundle.UserPrompt;

        Assert.Contains("\"a3\"", prompt);
        Assert.Contains("\"a4\"", prompt);
        Assert.Contains("\"a5\"", prompt);
        Assert.DoesNotContain("\"a1\"", prompt);
        Assert.DoesNotContain("\"a2\"", prompt);

        var idx5 = prompt.IndexOf("\"a5\"", StringComparison.Ordinal);
        var idx4 = prompt.IndexOf("\"a4\"", StringComparison.Ordinal);
        var idx3 = prompt.IndexOf("\"a3\"", StringComparison.Ordinal);
        Assert.True(idx5 < idx4, "a5 must come before a4 (most recent first)");
        Assert.True(idx4 < idx3, "a4 must come before a3");
    }

    [Fact]
    public void BuildBoardCluesPrompt_caps_per_direction_independently()
    {
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[]
            {
                new RejectedAttempt("top-1", "r"), new RejectedAttempt("top-2", "r"), new RejectedAttempt("top-3", "r"),
            },
            [Direction.Right] = new[]
            {
                new RejectedAttempt("right-1", "r"), new RejectedAttempt("right-2", "r"),
                new RejectedAttempt("right-3", "r"), new RejectedAttempt("right-4", "r"),
            },
        };

        var provider = new FrenchAiCluePromptProvider();
        var bundle = provider.BuildBoardCluesPrompt(SampleContext(rejected: rejected));
        var prompt = bundle.UserPrompt;

        Assert.Contains("\"top-1\"", prompt);
        Assert.Contains("\"top-2\"", prompt);
        Assert.Contains("\"top-3\"", prompt);

        Assert.DoesNotContain("\"right-1\"", prompt);
        Assert.Contains("\"right-2\"", prompt);
        Assert.Contains("\"right-3\"", prompt);
        Assert.Contains("\"right-4\"", prompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_partial_retry_omits_resolved_directions()
    {
        var bundle = new FrenchAiCluePromptProvider().BuildBoardCluesPrompt(
            SampleContext(remaining: new[] { Direction.Right, Direction.Left }));

        var prompt = bundle.UserPrompt;

        Assert.Contains("Carte TopLeft", prompt);
        Assert.Contains("Carte TopRight", prompt);

        var resolveSection = ExtractResolveSection(prompt);
        Assert.Contains("- Right", resolveSection);
        Assert.Contains("- Left", resolveSection);
        Assert.DoesNotContain("- Top", resolveSection);
        Assert.DoesNotContain("- Bottom", resolveSection);
    }

    private static void AssertDirectionPairs(string resolveSection, string direction, string word1, string word2)
    {
        var line = resolveSection
            .Split('\n')
            .FirstOrDefault(l => l.TrimStart().StartsWith($"- {direction}", StringComparison.Ordinal));

        Assert.True(line is not null, $"No '- {direction}' line found in resolve section.");
        Assert.Contains(word1, line!);
        Assert.Contains(word2, line!);
    }

    private static string ExtractResolveSection(string prompt)
    {
        var marker = "À résoudre dans cet appel :";
        var start = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        var end = prompt.IndexOf("Tous les mots", start, StringComparison.Ordinal);
        return end < 0 ? prompt[start..] : prompt[start..end];
    }

    [Fact]
    public void BuildBoardCluesPrompt_throws_when_cards_count_is_not_4()
    {
        var provider = new FrenchAiCluePromptProvider();
        var ctx = SampleContext() with
        {
            Cards = new[]
            {
                new BoardCardSnapshot(BoardPosition.TopLeft, "a", "b", "c", "d"),
            },
        };

        Assert.Throws<ArgumentException>(() => provider.BuildBoardCluesPrompt(ctx));
    }

    [Fact]
    public void BuildBoardCluesPrompt_throws_when_remaining_directions_is_empty()
    {
        var provider = new FrenchAiCluePromptProvider();
        var ctx = SampleContext(remaining: Array.Empty<Direction>());

        Assert.Throws<ArgumentException>(() => provider.BuildBoardCluesPrompt(ctx));
    }

    [Fact]
    public void BuildBoardCluesPrompt_throws_when_rejected_key_is_outside_remaining()
    {
        var provider = new FrenchAiCluePromptProvider();
        var rejected = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>
        {
            [Direction.Top] = new[] { new RejectedAttempt("x", "r") },
        };
        var ctx = SampleContext(
            remaining: new[] { Direction.Right, Direction.Left },
            rejected: rejected);

        Assert.Throws<ArgumentException>(() => provider.BuildBoardCluesPrompt(ctx));
    }

    [Fact]
    public void BuildDirectionsToResolve_emits_clean_per_direction_listing_without_card_face_annotation()
    {
        var bundle = new FrenchAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());
        var prompt = bundle.UserPrompt;

        var resolveSection = ExtractResolveSection(prompt);

        // Convention "faces extérieures" — on vérifie le mapping direction → paire de mots,
        // sans coupler au phrasé exact du prompt (susceptible de changer).
        // Top    → TopLeft.Top + TopRight.Top             = lune + vague
        // Right  → TopRight.Right + BottomRight.Right      = rocher + forêt
        // Bottom → BottomRight.Bottom + BottomLeft.Bottom  = montagne + ville
        // Left   → BottomLeft.Left + TopLeft.Left          = village + ciel
        AssertDirectionPairs(resolveSection, "Top", "lune", "vague");
        AssertDirectionPairs(resolveSection, "Right", "rocher", "forêt");
        AssertDirectionPairs(resolveSection, "Bottom", "montagne", "ville");
        AssertDirectionPairs(resolveSection, "Left", "village", "ciel");

        Assert.DoesNotContain("(carte", resolveSection);
        Assert.DoesNotContain("face Top)", resolveSection);
        Assert.DoesNotContain("face Right)", resolveSection);
        Assert.DoesNotContain("face Bottom)", resolveSection);
        Assert.DoesNotContain("face Left)", resolveSection);
    }

    [Fact]
    public void Bundle_exposes_PromptVersion_from_packaged_FR_template()
    {
        var provider = new FrenchAiCluePromptProvider();
        var bundle = provider.BuildBoardCluesPrompt(SampleContext());

        Assert.NotNull(bundle.PromptVersion);
        Assert.True(bundle.PromptVersion!.Value >= 1,
            $"Expected PromptVersion >= 1, got {bundle.PromptVersion}.");
    }

    [Fact]
    public void BuildBoardCluesPrompt_user_prompt_describes_explanation_field_as_reasoning()
    {
        var bundle = new FrenchAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());

        // On vérifie que le prompt demande bien un raisonnement (concept stable),
        // sans coupler au phrasé exact de la consigne.
        Assert.Contains("raisonnement", bundle.UserPrompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_includes_reasoning_block_in_system_when_reasoning_enabled()
    {
        var bundle = new FrenchAiCluePromptProvider()
            .BuildBoardCluesPrompt(SampleContext() with { IncludeReasoning = true });

        Assert.Contains("de façon ramassée", bundle.SystemPrompt);
    }

    [Fact]
    public void BuildBoardCluesPrompt_omits_reasoning_block_when_reasoning_disabled_by_default()
    {
        var bundle = new FrenchAiCluePromptProvider().BuildBoardCluesPrompt(SampleContext());

        Assert.DoesNotContain("de façon ramassée", bundle.SystemPrompt);
    }
}
