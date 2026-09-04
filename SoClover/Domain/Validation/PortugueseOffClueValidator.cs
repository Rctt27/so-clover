namespace SoClover.Domain.Validation;

/// <summary>
/// Portuguese OFF validator (Brazilian register, matching the packaged dictionary). Applies R1 only
/// (bidirectional substring). The French R2 "vowel stem" heuristic is intentionally NOT applied:
/// Portuguese nouns overwhelmingly end in a vowel (<i>casa</i>, <i>mesa</i>, <i>carro</i>), so
/// stripping it and re-testing the substring relation would fire on nearly every board word and
/// produce false positives ("mesa" → stem "mes" wrongly rejecting "mesmo"). Root-sharing is covered
/// by the LLM prompt, same trade-off as <see cref="EnglishOffClueValidator"/>.
/// </summary>
public sealed class PortugueseOffClueValidator : SubstringClueValidator
{
    public override string Language => "Portuguese_(from_FR_OFF)";
}
