namespace SoClover.Infrastructure.AI.Prompts;

public sealed class FrenchAiCluePromptProvider : FileAiCluePromptProvider
{
    private static readonly AiCluePromptLabels Labels = new(
        CardLineFormat: "- Carte {0} : Top=\"{1}\" Right=\"{2}\" Bottom=\"{3}\" Left=\"{4}\"",
        DirectionLineFormat: "- {0} : trouve un mot-indice qui évoque à la fois \"{1}\" et \"{2}\"",
        RetryDirectionFormat: "Direction {0} :",
        RetryAttemptFormat: "  - \"{0}\" rejeté ({1})");

    public FrenchAiCluePromptProvider()
        : this(new FilePromptLoader(), DefaultPromptPath()) { }

    internal FrenchAiCluePromptProvider(FilePromptLoader loader, string promptFilePath)
        : base(loader, promptFilePath, Labels, "Français_OFF") { }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "fr", "board-clues.md");
    }
}
