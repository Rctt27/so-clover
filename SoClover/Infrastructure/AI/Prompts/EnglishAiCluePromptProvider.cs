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
        : this(new FilePromptLoader(), DefaultPromptPath()) { }

    internal EnglishAiCluePromptProvider(FilePromptLoader loader, string promptFilePath)
        : base(loader, promptFilePath, Labels, "English_(from_FR_OFF)") { }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "en", "board-clues.md");
    }
}
