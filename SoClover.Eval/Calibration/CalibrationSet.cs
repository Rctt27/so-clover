using SoClover.Eval.Human;
using SoClover.Eval.Io;

namespace SoClover.Eval.Calibration;

/// <summary>Un indice à décoder, identifié par sa direction — la paire de référence en dépend.</summary>
public sealed record CalibrationClue(string BoardId, string Direction, string Clue);

/// <summary>
/// Un couple à trancher par le décodeur. <see cref="SourceA"/>/<see cref="SourceB"/> sont conservés
/// parce que le verdict d'une <b>ancre</b> ne se lit qu'à travers eux : le décodeur a raison quand
/// il préfère l'indice réel à l'aléatoire.
/// </summary>
public sealed record CalibrationCouple(
    string ComparisonId,
    string Family,
    string BoardId,
    string Direction,
    string SourceA,
    string SourceB,
    string ClueA,
    string ClueB,
    string HumanVerdict);

public sealed record CalibrationLot(
    IReadOnlyList<CalibrationCouple> Couples,
    IReadOnlyList<CalibrationCouple> Anchors,
    IReadOnlyList<CalibrationClue> Clues);

/// <summary>
/// De <c>comparisons.dev.jsonl</c> vers le lot de calibration : couples retenus et indices
/// distincts à décoder.
/// <para>
/// Trois pièges y sont désamorcés une bonne fois : un <b>doublon inversé</b> désigne le même
/// couple et ne le compte qu'une fois (dernier verdict retenu — « la dernière ligne gagne », la
/// règle de lecture déjà en place dans <see cref="HumanFile"/>) ; les <b>ancres</b> sortent du
/// calcul principal ; et un indice partagé par deux couples n'est <b>décodé qu'une fois</b>.
/// </para>
/// </summary>
public static class CalibrationSet
{
    /// <summary>
    /// Les familles qui entrent dans le calcul principal. <c>anchor</c> en est exclue et
    /// rapportée à part : y inclure les ancres reviendrait à mesurer l'accord sur des couples
    /// dont l'écart de qualité est évident — exactement ce que le design P4-P5 refusait déjà de
    /// faire côté humain.
    /// </summary>
    public static readonly IReadOnlyList<string> MainFamilies =
    [
        ComparisonFamilies.HumanVsModel,
        ComparisonFamilies.ModelVsModel,
        ComparisonFamilies.HumanVsAssisted,
    ];

    public static CalibrationLot Build(ComparisonContents contents)
    {
        // Un re-jugement ajoute une ligne sous le même comparisonId : seul le dernier compte.
        var latest = HumanFile.LatestByComparisonId(contents);

        // Puis le doublon inversé : il porte « <original>-r » et DuplicateOf = « <original> ».
        // Les deux lignes parlent du même couple — on les replie sur la clé de l'original, en
        // gardant la dernière apparition dans le fichier.
        var byCouple = new Dictionary<string, ComparisonLine>(StringComparer.Ordinal);
        foreach (var line in contents.Comparisons)
        {
            if (!latest.TryGetValue(line.ComparisonId, out var kept) || !ReferenceEquals(kept, line))
                continue;

            byCouple[line.DuplicateOf ?? line.ComparisonId] = line;
        }

        var couples = new List<CalibrationCouple>();
        var anchors = new List<CalibrationCouple>();

        foreach (var (coupleId, line) in byCouple
                     .OrderBy(kv => kv.Value.BoardId, StringComparer.Ordinal)
                     .ThenBy(kv => kv.Value.Direction, StringComparer.Ordinal)
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                     .Select(kv => (kv.Key, kv.Value)))
        {
            // Rien à trancher : deux indices identiques ne portent aucune préférence, et le
            // décodeur leur donnerait mécaniquement le même R̄.
            if (string.Equals(line.OptionA.Clue.Trim(), line.OptionB.Clue.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var couple = new CalibrationCouple(
                ComparisonId: coupleId,
                Family: line.Family,
                BoardId: line.BoardId,
                Direction: line.Direction,
                SourceA: line.OptionA.Source,
                SourceB: line.OptionB.Source,
                ClueA: line.OptionA.Clue,
                ClueB: line.OptionB.Clue,
                HumanVerdict: line.Verdict);

            if (line.Family == ComparisonFamilies.Anchor)
                anchors.Add(couple);
            else
                couples.Add(couple);
        }

        var clues = couples.Concat(anchors)
            .SelectMany(c => new[]
            {
                new CalibrationClue(c.BoardId, c.Direction, c.ClueA),
                new CalibrationClue(c.BoardId, c.Direction, c.ClueB),
            })
            .Distinct()
            .OrderBy(c => c.BoardId, StringComparer.Ordinal)
            .ThenBy(c => c.Direction, StringComparer.Ordinal)
            .ThenBy(c => c.Clue, StringComparer.Ordinal)
            .ToList();

        return new CalibrationLot(couples.AsReadOnly(), anchors.AsReadOnly(), clues.AsReadOnly());
    }

    /// <summary>
    /// R̄ par indice, calculé sur les décodages <b>valides</b>. Un décodage en échec de format est
    /// <b>exclu du dénominateur</b>, jamais compté 0 — invariant déjà tenu par <c>RunMetrics</c>,
    /// et pour la même raison : un décodeur qui ne sait pas répondre au format n'est pas un
    /// décodeur qui se trompe.
    /// <para>
    /// <c>null</c> quand aucun décodage n'est exploitable : R̄ est alors <b>indéfini</b>, et le
    /// couple concerné sortira du calcul plutôt que d'être compté <c>tie</c> ou perdant.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<CalibrationClue, double?> RBarByClue(CalibrationContents decodes) =>
        decodes.Decodes
            .GroupBy(d => new CalibrationClue(d.BoardId, d.Direction, d.Clue))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var scored = g.Where(d => d.DecodeFailureKind is null && d.R is not null).ToList();
                    return scored.Count == 0 ? (double?)null : scored.Average(d => d.R!.Value);
                });
}
