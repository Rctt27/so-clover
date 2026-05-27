namespace SoClover.Domain.Validation;

public sealed class FrenchOffClueValidator : SubstringClueValidator
{
    public override string Language => "Français_OFF";

    private static readonly HashSet<char> Voyelles = new() { 'a', 'e', 'i', 'o', 'u', 'y' };

    // R2 — French morphology heuristic: strip a single trailing vowel from the card word, then re-test
    // the substring relation. Catches "naturiste" against "nature" (stem "natur").
    protected override void CheckAdditionalRules(
        string clueNorm,
        string wordNorm,
        string word,
        Direction? wordDirection,
        List<ClueValidationError> errors)
    {
        if (!Voyelles.Contains(wordNorm[^1]))
            return;

        var stem = wordNorm[..^1];
        if (stem.Length < MinWordLength)
            return;

        if (clueNorm.Contains(stem, StringComparison.Ordinal)
            || (clueNorm.Length >= MinWordLength && stem.Contains(clueNorm, StringComparison.Ordinal)))
        {
            errors.Add(new ClueValidationError(ClueValidationRule.SimilarStem, word, wordDirection));
        }
    }
}
