namespace SoClover.Infrastructure.AI.Prompts;

public sealed class EnglishAiCluePromptProvider : FileAiCluePromptProvider
{
    private static readonly AiCluePromptLabels Labels = new(
        CardLineFormat: "- Card {0}: Top=\"{1}\" Right=\"{2}\" Bottom=\"{3}\" Left=\"{4}\"",
        DirectionLineFormat: "- {0}: find a clue word that evokes both \"{1}\" and \"{2}\"",
        RetryDirectionFormat: "Direction {0}:",
        RetryAttemptFormat: "  - \"{0}\" rejected ({1})",
        RejectionRuleWithDirectionFormat: "{0} with the word \"{1}\" (direction {2})",
        RejectionRuleFormat: "{0} with the word \"{1}\"");

    public EnglishAiCluePromptProvider()
        : this(new FilePromptLoader(), DefaultPromptPath(), DefaultPerDirectionPromptPath()) { }

    internal EnglishAiCluePromptProvider(
        FilePromptLoader loader, string promptFilePath, string perDirectionPromptFilePath)
        : base(loader, promptFilePath, perDirectionPromptFilePath, Labels, "English_(from_FR_OFF)") { }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "en", "board-clues.md");
    }

    private static string DefaultPerDirectionPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "en", "board-clues-per-direction.md");
    }
}
