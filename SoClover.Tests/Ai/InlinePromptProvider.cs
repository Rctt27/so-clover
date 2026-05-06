using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Tests.AI;

/// <summary>
/// Test-only implementation of <see cref="IAiCluePromptProvider"/> that returns a fixed
/// bundle without touching the file system. Use when an Epic-06 test wants to exercise
/// the LLM call pipeline without depending on the FR prompt file or its placeholder logic.
/// </summary>
public sealed class InlinePromptProvider : IAiCluePromptProvider
{
    private readonly Func<BoardCluesPromptContext, AiCluePromptBundle> _build;

    public InlinePromptProvider(string language, Func<BoardCluesPromptContext, AiCluePromptBundle> build)
    {
        Language = language;
        _build = build;
    }

    public InlinePromptProvider(string language, AiCluePromptBundle fixedBundle)
        : this(language, _ => fixedBundle) { }

    public string Language { get; }

    public AiCluePromptBundle BuildBoardCluesPrompt(BoardCluesPromptContext context)
        => _build(context);
}
