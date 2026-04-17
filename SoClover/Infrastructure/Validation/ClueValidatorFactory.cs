using SoClover.Domain;
using SoClover.Domain.Validation;

namespace SoClover.Infrastructure.Validation;

public sealed class ClueValidatorFactory : IClueValidatorFactory
{
    private static readonly FrenchOffClueValidator French = new();

    public IClueValidator GetFor(string language, bool semanticCheckEnabled)
    {
        if (!semanticCheckEnabled)
            return NullClueValidator.Instance;

        // Match case-insensitively so "francais_off" or "Français_OFF" both resolve to FR
        var norm = TextNormalizer.Normalize(language);
        if (norm.StartsWith("francais", StringComparison.Ordinal))
            return French;

        return NullClueValidator.Instance;
    }
}