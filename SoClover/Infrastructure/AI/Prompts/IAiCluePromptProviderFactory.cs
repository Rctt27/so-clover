namespace SoClover.Infrastructure.AI.Prompts;

public interface IAiCluePromptProviderFactory
{
    /// <summary>
    /// Returns the provider matching the given language, or null if unsupported.
    /// Caller (Epic 02/06) is responsible for translating null into UnsupportedAiLanguageException.
    /// </summary>
    IAiCluePromptProvider? GetFor(string language);

    bool IsLanguageSupported(string language);
}
