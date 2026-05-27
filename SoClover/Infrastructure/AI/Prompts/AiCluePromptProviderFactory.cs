using SoClover.Domain;

namespace SoClover.Infrastructure.AI.Prompts;

public sealed class AiCluePromptProviderFactory : IAiCluePromptProviderFactory
{
    private static readonly FrenchAiCluePromptProvider French = new();
    private static readonly EnglishAiCluePromptProvider English = new();

    public IAiCluePromptProvider? GetFor(string language)
    {
        var norm = TextNormalizer.Normalize(language);
        if (norm.StartsWith("francais", StringComparison.Ordinal))
            return French;
        if (norm.StartsWith("english", StringComparison.Ordinal))
            return English;

        return null;
    }

    public bool IsLanguageSupported(string language) => GetFor(language) is not null;
}
