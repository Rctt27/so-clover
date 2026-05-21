using SoClover.Domain;

namespace SoClover.Infrastructure.AI.Prompts;

public sealed class AiCluePromptProviderFactory : IAiCluePromptProviderFactory
{
    private static readonly FrenchAiCluePromptProvider French = new();

    public IAiCluePromptProvider? GetFor(string language)
    {
        var norm = TextNormalizer.Normalize(language);
        if (norm.StartsWith("francais", StringComparison.Ordinal))
            return French;

        return null;
    }

    public bool IsLanguageSupported(string language) => GetFor(language) is not null;
}
