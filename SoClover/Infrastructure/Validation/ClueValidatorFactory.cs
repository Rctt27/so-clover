using SoClover.Domain;
using SoClover.Domain.Validation;

namespace SoClover.Infrastructure.Validation;

public sealed class ClueValidatorFactory : IClueValidatorFactory
{
    private static readonly FrenchOffClueValidator French = new();
    private static readonly EnglishOffClueValidator English = new();

    public IClueValidator GetFor(string language, bool semanticCheckEnabled)
    {
        if (!semanticCheckEnabled)
            return NullClueValidator.Instance;

        // Match case/diacritics-insensitively so "francais_off" or "Français_OFF" both resolve to FR.
        var norm = TextNormalizer.Normalize(language);
        if (norm.StartsWith("francais", StringComparison.Ordinal))
            return French;
        if (norm.StartsWith("english", StringComparison.Ordinal))
            return English;

        return NullClueValidator.Instance;
    }
}
