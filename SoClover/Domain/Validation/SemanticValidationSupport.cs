namespace SoClover.Domain.Validation;

/// <summary>
/// Single source of truth for which dictionaries support semantic clue conformity validation.
/// Both the domain gate (<see cref="Game"/>) and the validator factory consult this so the set of
/// languages stays in sync as new dictionaries are added.
/// </summary>
public static class SemanticValidationSupport
{
    private static readonly string[] SupportedPrefixes = { "francais", "english" };

    public static bool IsSupported(string language)
    {
        var norm = TextNormalizer.Normalize(language);
        return SupportedPrefixes.Any(prefix => norm.StartsWith(prefix, StringComparison.Ordinal));
    }
}
