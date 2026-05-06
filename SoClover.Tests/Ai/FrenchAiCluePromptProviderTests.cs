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
            var provider = new FrenchAiCluePromptProvider(new FilePromptLoader(), fixturePath);
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
            Assert.Contains("plage", bundle.UserPrompt);
            Assert.Contains("sable", bundle.UserPrompt);

            Assert.DoesNotContain("rejeté", bundle.UserPrompt, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("{{", bundle.UserPrompt);
            Assert.DoesNotContain("}}", bundle.UserPrompt);

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
}
