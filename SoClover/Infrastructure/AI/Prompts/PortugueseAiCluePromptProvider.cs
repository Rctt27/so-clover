namespace SoClover.Infrastructure.AI.Prompts;

public sealed class PortugueseAiCluePromptProvider : FileAiCluePromptProvider
{
    // Brazilian Portuguese, matching the register of the packaged Portuguese_(from_FR_OFF) dictionary.
    private static readonly AiCluePromptLabels Labels = new(
        CardLineFormat: "- Carta {0}: Top=\"{1}\" Right=\"{2}\" Bottom=\"{3}\" Left=\"{4}\"",
        DirectionLineFormat: "- {0}: encontre uma palavra-pista que evoque tanto \"{1}\" quanto \"{2}\"",
        RetryDirectionFormat: "Direção {0}:",
        RetryAttemptFormat: "  - \"{0}\" rejeitada ({1})",
        RejectionRuleWithDirectionFormat: "{0} com a palavra \"{1}\" (direção {2})",
        RejectionRuleFormat: "{0} com a palavra \"{1}\"",
        TooLongFormat: "pista longa demais (máx. {0} caracteres)");

    public PortugueseAiCluePromptProvider()
        : this(
            new FilePromptLoader(),
            DefaultPromptPath(),
            DefaultPerDirectionPromptPath(),
            DefaultPerDirectionReasoningPromptPath()) { }

    internal PortugueseAiCluePromptProvider(
        FilePromptLoader loader,
        string promptFilePath,
        string perDirectionPromptFilePath,
        string? perDirectionReasoningPromptFilePath = null)
        : base(
            loader,
            promptFilePath,
            perDirectionPromptFilePath,
            Labels,
            "Portuguese_(from_FR_OFF)",
            perDirectionReasoningPromptFilePath) { }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "pt", "board-clues.md");
    }

    private static string DefaultPerDirectionPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "pt", "board-clues-per-direction.md");
    }

    private static string DefaultPerDirectionReasoningPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(
            baseDir, "Infrastructure", "AI", "Prompts", "pt", "board-clues-per-direction.reasoning.md");
    }
}
