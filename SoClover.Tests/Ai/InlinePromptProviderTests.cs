using SoClover.Domain;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class InlinePromptProviderTests
{
    private static BoardCluesPromptContext MinimalContext(string language) =>
        new(
            Language: language,
            Cards: new[]
            {
                new BoardCardSnapshot(BoardPosition.TopLeft,     "a1", "a2", "a3", "a4"),
                new BoardCardSnapshot(BoardPosition.TopRight,    "b1", "b2", "b3", "b4"),
                new BoardCardSnapshot(BoardPosition.BottomRight, "c1", "c2", "c3", "c4"),
                new BoardCardSnapshot(BoardPosition.BottomLeft,  "d1", "d2", "d3", "d4"),
            },
            RemainingDirections: new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
            RejectedPerDirection: new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());

    [Fact]
    public void Returns_fixed_bundle()
    {
        var bundle = new AiCluePromptBundle("sys", "user", "{}");
        var provider = new InlinePromptProvider("Français_OFF", bundle);

        var result = provider.BuildBoardCluesPrompt(MinimalContext("Français_OFF"));

        Assert.Equal(bundle, result);
        Assert.Equal("Français_OFF", provider.Language);
    }

    [Fact]
    public void Builder_func_receives_context()
    {
        BoardCluesPromptContext? captured = null;
        var provider = new InlinePromptProvider("X", ctx =>
        {
            captured = ctx;
            return new AiCluePromptBundle("s", "u", "{}");
        });

        var ctx = MinimalContext("X");

        provider.BuildBoardCluesPrompt(ctx);

        Assert.Equal(ctx, captured);
    }
}
