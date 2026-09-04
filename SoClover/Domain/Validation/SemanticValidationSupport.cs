namespace SoClover.Domain.Validation;

/// <summary>
/// Single source of truth for which dictionaries support semantic clue conformity validation, AND
/// for which validator serves each one. The domain gate (<see cref="Game"/>) and the infrastructure
/// factory both resolve through this table, so the supported set cannot drift between them: adding
/// a language is one entry here.
/// </summary>
public static class SemanticValidationSupport
{
    // Prefixes are matched against TextNormalizer.Normalize(language), which lowercases and strips
    // diacritics — so "portugues" covers both "Portuguese_(from_FR_OFF)" and "Português", the same
    // way "francais" covers "Français_OFF" and "francais_off". Validators are stateless singletons.
    private static readonly (string Prefix, IClueValidator Validator)[] ByPrefix =
    {
        ("francais", new FrenchOffClueValidator()),
        ("english", new EnglishOffClueValidator()),
        ("portugues", new PortugueseOffClueValidator()),
    };

    /// <summary>Validator for <paramref name="language"/>, or null when that dictionary has none.</summary>
    public static IClueValidator? Find(string? language)
    {
        var norm = TextNormalizer.Normalize(language);
        if (norm.Length == 0)
            return null;

        foreach (var (prefix, validator) in ByPrefix)
        {
            if (norm.StartsWith(prefix, StringComparison.Ordinal))
                return validator;
        }

        return null;
    }

    public static bool IsSupported(string language) => Find(language) is not null;
}
