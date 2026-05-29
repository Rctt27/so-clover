namespace SoClover.Infrastructure.AI.Prompts;

public sealed class FrenchAiCluePromptProvider : FileAiCluePromptProvider
{
    private static readonly AiCluePromptLabels Labels = new(
        CardLineFormat: "- Carte {0} : Top=\"{1}\" Right=\"{2}\" Bottom=\"{3}\" Left=\"{4}\"",
        DirectionLineFormat: "- {0} : trouve un mot-indice qui évoque à la fois \"{1}\" et \"{2}\"",
        RetryDirectionFormat: "Direction {0} :",
        RetryAttemptFormat: "  - \"{0}\" rejeté ({1})",
        RejectionRuleWithDirectionFormat: "{0} avec le mot \"{1}\" (direction {2})",
        RejectionRuleFormat: "{0} avec le mot \"{1}\"");

    public FrenchAiCluePromptProvider()
        : this(
            new FilePromptLoader(),
            DefaultPromptPath(),
            DefaultPerDirectionPromptPath(),
            DefaultPerDirectionReasoningPromptPath()) { }

    internal FrenchAiCluePromptProvider(
        FilePromptLoader loader,
        string promptFilePath,
        string perDirectionPromptFilePath,
        string? perDirectionReasoningPromptFilePath = null)
        : base(
            loader,
            promptFilePath,
            perDirectionPromptFilePath,
            Labels,
            "Français_OFF",
            perDirectionReasoningPromptFilePath) { }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "fr", "board-clues.md");
    }

    private static string DefaultPerDirectionPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "fr", "board-clues-per-direction.md");
    }

    private static string DefaultPerDirectionReasoningPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(
            baseDir, "Infrastructure", "AI", "Prompts", "fr", "board-clues-per-direction.reasoning.md");
    }
}
