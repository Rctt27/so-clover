using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

/// <summary>
/// Restriction des métriques au sous-ensemble de directions réellement couvertes par une séance
/// humaine. <b>Ce n'est pas un confort, c'est une correction de dénominateur</b> :
/// <c>RunMetrics.Compute</c> construit ses items depuis le banc entier et attribue R̄ = 0 aux
/// directions absentes — comportement voulu pour un run de modèle, où une direction non générée
/// est un échec. Appliqué tel quel à un run humain couvrant 40 directions sur 160, il rendrait un
/// plafond humain <b>divisé par quatre</b>, faux et parfaitement plausible.
/// </summary>
public static class SubsetSelector
{
    /// <summary>
    /// Issues retenues. Sans drapeau, <b>toutes</b> — y compris <c>pass</c>, qui reste au
    /// dénominateur conformément à A-1 : c'est le « plafond joué ». Avec
    /// <c>solide,tiede</c>, c'est le « plafond mesuré sur les paires résolues » d'A-3.
    /// </summary>
    public static IReadOnlySet<string> ParseOutcomes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(Outcomes.All, StringComparer.Ordinal);

        var requested = raw
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var unknown = requested.Where(o => !Outcomes.IsKnown(o)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException(
                $"--subset-outcome : issue(s) inconnue(s) « {string.Join(", ", unknown)} ». " +
                $"Vocabulaire fermé : {string.Join(", ", Outcomes.All)}.");

        return new HashSet<string>(requested, StringComparer.Ordinal);
    }

    /// <summary>
    /// Périmètre d'issues effectif, trié, tel qu'il part dans le <c>.metrics.json</c>.
    /// <para>
    /// Toujours renseigné, même sans drapeau : « toutes les issues » écrit
    /// <c>pass,solide,tiede</c> plutôt que <c>null</c>. C'est ce qui permet à
    /// <c>CalibrationGates.RequireSaturationSubset</c> de distinguer un périmètre déclaré d'un
    /// fichier antérieur à la provenance — le premier se vérifie, le second ne prouve rien. Le tri
    /// garantit qu'un même périmètre ne produit pas deux provenances selon l'ordre de saisie.
    /// </para>
    /// </summary>
    internal static string Normalize(IReadOnlySet<string> outcomes) =>
        string.Join(",", outcomes.OrderBy(o => o, StringComparer.Ordinal));

    /// <summary>
    /// Les directions écrites par la séance, filtrées sur les issues demandées. Une direction
    /// jamais saisie n'appartient à aucun sous-ensemble : elle n'est pas un échec humain, elle
    /// n'a simplement pas été posée.
    /// </summary>
    public static IReadOnlySet<(string BoardId, string Direction)> FromElicitation(
        ElicitationContents elicitation, IReadOnlySet<string> outcomes)
    {
        var subset = new HashSet<(string BoardId, string Direction)>();
        foreach (var line in elicitation.Elicitations.Where(l => outcomes.Contains(l.Outcome)))
            subset.Add((line.BoardId, line.Direction));

        return subset;
    }

    /// <summary>
    /// Lecture depuis le disque pour la CLI. Rend aussi le <b>nom court</b> du fichier, qui part
    /// dans la cellule <i>réglages</i> du registre : aucune ligne ne peut prétendre porter sur le
    /// banc entier alors qu'elle porte sur un quart.
    /// </summary>
    public static (IReadOnlySet<(string BoardId, string Direction)> Subset, string Name, string? Outcome)
        FromFile(string path, string? outcomeFilter)
    {
        var elicitation = HumanFile.ReadElicitation(path);
        var outcomes = ParseOutcomes(outcomeFilter);
        return (FromElicitation(elicitation, outcomes), Path.GetFileName(path), Normalize(outcomes));
    }
}
