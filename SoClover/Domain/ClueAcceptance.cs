using SoClover.Domain.Validation;

namespace SoClover.Domain;

/// <summary>
/// Séquence d'acceptation d'un indice, extraite verbatim de <see cref="Game.SetClue"/> :
/// trim → plafond <see cref="Game.MaxClueLength"/> → <see cref="ClueText.Create"/> → validateur.
/// <para>
/// Le validateur seul ne suffit pas : il ignore le plafond de longueur, appliqué en amont par
/// le jeu. Le harnais d'évaluation appelle cette méthode plutôt que le validateur directement,
/// pour que son <c>valid_rate</c> mesure exactement ce que la prod accepte.
/// </para>
/// </summary>
public static class ClueAcceptance
{
    /// <exception cref="InvalidClueException">L'indice est vide ou uniquement composé d'espaces.</exception>
    public static ClueValidationResult Check(
        string clueText, Direction direction, CloverBoard board, IClueValidator validator)
    {
        var trimmed = (clueText ?? string.Empty).Trim();
        if (trimmed.Length > Game.MaxClueLength)
        {
            return ClueValidationResult.Invalid(
                new ClueValidationError(ClueValidationRule.TooLong, string.Empty, null, Game.MaxClueLength));
        }

        var parsed = ClueText.Create(clueText);
        return validator.Validate(parsed.Value, direction, board);
    }
}
