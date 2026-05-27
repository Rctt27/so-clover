namespace SoClover.Domain.Validation;

/// <summary>
/// English OFF validator. Applies R1 only (bidirectional substring). The French R2 "vowel stem"
/// heuristic is intentionally NOT applied: English morphology differs (trailing 'e' is often
/// structural, plural/derivation suffixes are not vowel-final), so R2 would produce false positives
/// (e.g. "core" → stem "cor" would wrongly reject "corner"). Root-sharing is covered by the LLM prompt.
/// </summary>
public sealed class EnglishOffClueValidator : SubstringClueValidator
{
    public override string Language => "English_(from_FR_OFF)";
}
