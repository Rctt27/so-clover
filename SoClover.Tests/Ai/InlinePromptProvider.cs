using SoClover.Domain.Validation;
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
    private readonly Func<BoardCluesPromptContext, AiCluePromptBundle> _buildSingle;

    public InlinePromptProvider(
        string language,
        Func<BoardCluesPromptContext, AiCluePromptBundle> build,
        Func<BoardCluesPromptContext, AiCluePromptBundle>? buildSingle = null)
    {
        Language = language;
        _build = build;
        _buildSingle = buildSingle ?? build;
    }

    public InlinePromptProvider(string language, AiCluePromptBundle fixedBundle)
        : this(language, _ => fixedBundle) { }

    public string Language { get; }

    public AiCluePromptBundle BuildBoardCluesPrompt(BoardCluesPromptContext context)
        => _build(context);

    public AiCluePromptBundle BuildSingleDirectionCluePrompt(BoardCluesPromptContext context)
    {
        if (context.RemainingDirections.Count != 1)
            throw new ArgumentException(
                "BuildSingleDirectionCluePrompt requires exactly 1 remaining direction.",
                nameof(context));
        return _buildSingle(context);
    }

    public string FormatRejectionReason(ClueValidationResult result)
        => string.Join("; ", result.Errors.Select(e => $"{e.Rule}:{e.CardWord}"));
}
