using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Analysis;

/// <summary>
/// Vocabulaire <b>fermé</b> des modes d'échec. Les codes viennent du PRD ; les libellés sont
/// affichés dans le rapport et dans l'échantillon relu à la main.
/// </summary>
public static class FailureModes
{
    public const string M0 = "M0";
    public const string M1 = "M1";
    public const string M2 = "M2";
    public const string M3 = "M3";
    public const string M4 = "M4";
    public const string M5 = "M5";
    public const string M6 = "M6";

    /// <summary>Une taxonomie qui classe 100 % des items est une taxonomie qui triche.</summary>
    public const string Unclassified = "M?";

    public static readonly IReadOnlyList<string> All = [M0, M1, M2, M3, M4, M5, M6, Unclassified];

    public static string Label(string mode) => mode switch
    {
        M0 => "réussi",
        M1 => "trop générique",
        M2 => "n'attrape qu'une face",
        M3 => "collision avec un distracteur",
        M4 => "relation trop indirecte",
        M5 => "jargon / mot rare (manuel)",
        M6 => "collision inter-directions (board)",
        Unclassified => "non classé",
        _ => mode,
    };

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public sealed record DirectionLabel(
    string BoardId, string Direction, string Mode, double RBar, int ScoredDecodeCount);

public sealed record ModeCount(string Mode, string Label, int Count, double Share, bool ActionJustified);

public sealed record TaxonomyReport(
    string RunId,
    int DirectionCount,
    int ScorableDirectionCount,
    int UnscorableDirectionCount,
    IReadOnlyList<DirectionLabel> Labels,
    IReadOnlyList<ModeCount> Distribution,
    int BoardCount,
    int M6BoardCount,
    double M6Share,
    IReadOnlyList<string> M6Boards);

/// <summary>
/// Taxonomie chiffrée des modes d'échec. Chaque direction reçoit <b>une</b> étiquette.
/// <para>
/// <b>L'ordre de priorité est déterministe et documenté</b> : <c>M2 → M3 → M4 → M1 → M?</c>. Les
/// signatures se recouvrent — une direction peut être à la fois dispersée et concentrée sur un
/// mot — et une taxonomie dont l'ordre d'évaluation n'est pas écrit produit des distributions non
/// reproductibles.
/// </para>
/// <para>
/// <c>M6</c> est compté <b>séparément, en boards</b>, jamais mélangé à la distribution par
/// direction : ce n'est pas la même unité. <c>M5</c> n'est <b>jamais</b> produit
/// automatiquement — voir <see cref="LabelDirection"/>.
/// </para>
/// </summary>
public static class FailureTaxonomy
{
    /// <summary>Au-dessus, la direction est réussie (<c>M0</c>).</summary>
    public const double SuccessThreshold = 0.75;

    /// <summary>« ≥ 5 % → intervention justifiée » / « &lt; 5 % → aucune ligne de prompt ».</summary>
    public const double ActionThreshold = 0.05;

    /// <summary><c>M1</c> : mots faux <b>dispersés</b>.</summary>
    public const int DispersionThreshold = 4;

    /// <summary><c>M2</c> et <c>M3</c> : signature <b>concentrée</b>, répétée.</summary>
    public const int ConcentrationThreshold = 2;

    public const double M6MinBoardMean = 0.50;
    public const double M6MinGap = 0.20;

    /// <summary>
    /// Sous ce nombre de directions exploitables, une moyenne « au niveau board » n'est plus la
    /// même mesure : le board est écarté de M6, jamais imputé à 0.
    /// </summary>
    public const int M6MinExploitableDirections = 3;

    /// <summary>
    /// L'ordre d'évaluation, exposé pour être testable et lisible dans le rapport.
    /// <para>
    /// <b>M4 précède M1</b>, contrairement à l'ordre littéral du design. Sous l'ordre
    /// <c>M1 → M4</c>, M4 serait <b>structurellement inatteignable</b> : R̄ = 0 implique deux mots
    /// faux par décodage, et une intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux
    /// distincts — donc M1 à tous les coups. R̄ = 0 est la condition strictement plus forte : elle
    /// passe devant.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> PriorityOrder =
        [FailureModes.M2, FailureModes.M3, FailureModes.M4, FailureModes.M1];

    public static TaxonomyReport Compute(BenchContents bench, RunContents run, DecodeContents decoded)
    {
        var decodesByItem = decoded.ClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ClueDecodeLine>)g.OrderBy(d => d.DecodeIndex).ToList());

        var boardPositionsByBoard = decoded.BoardDecodes
            .Where(b => b.DecodeFailureKind is null && b.BoardPositions is not null)
            .GroupBy(b => b.BoardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().BoardPositions!.Value, StringComparer.Ordinal);

        var labels = new List<DirectionLabel>();
        var m6Boards = new List<string>();

        foreach (var board in bench.Boards)
        {
            // I2 : SEULES les directions exploitables entrent dans la moyenne board. Une
            // direction D6 n'a pas de R̄ « nul » — elle n'a pas de R̄ du tout ; l'imputer à 0
            // biaiserait la moyenne à la baisse (faux négatifs M6).
            var exploitableRBar = new List<double>(4);

            foreach (var direction in BoardGeometry.AllDirections)
            {
                var key = (board.BoardId, direction.ToString());
                var decodes = decodesByItem.TryGetValue(key, out var d) ? d : [];
                var reference = BenchBoardMapper.ReferenceWords(board, direction);

                var (mode, rBar, scored) = LabelDirection(reference, decodes);
                labels.Add(new DirectionLabel(board.BoardId, direction.ToString(), mode, rBar, scored));
                if (scored > 0)
                    exploitableRBar.Add(rBar);
            }

            // M6 : signature AU NIVEAU BOARD. Les indices marchent un par un, mais mis ensemble
            // ils se disputent les mêmes mots. Sous M6MinExploitableDirections, ce n'est plus la
            // même mesure : le board est écarté, jamais imputé.
            if (exploitableRBar.Count >= M6MinExploitableDirections
                && boardPositionsByBoard.TryGetValue(board.BoardId, out var boardPositions)
                && exploitableRBar.Average() >= M6MinBoardMean
                && exploitableRBar.Average() - boardPositions >= M6MinGap)
                m6Boards.Add(board.BoardId);
        }

        // D6/I1 : le dénominateur des parts est le nombre de directions EXPLOITABLES, jamais le
        // total. Une direction D6 (aucun décodage exploitable) n'entre dans AUCUNE part — ni
        // comme numérateur, ni comme dénominateur : elle est rapportée à part, via
        // UnscorableDirectionCount, jamais mélangée à la distribution par mode.
        var scorableLabels = labels.Where(l => l.ScoredDecodeCount > 0).ToList();
        var scorable = scorableLabels.Count;
        var distribution = FailureModes.All
            .Where(m => m != FailureModes.M6)
            .Select(m =>
            {
                var count = scorableLabels.Count(l => l.Mode == m);
                var share = scorable == 0 ? 0.0 : count / (double)scorable;
                return new ModeCount(m, FailureModes.Label(m), count, share, share >= ActionThreshold);
            })
            .ToList()
            .AsReadOnly();

        return new TaxonomyReport(
            RunId: run.Manifest.RunId,
            DirectionCount: labels.Count,
            ScorableDirectionCount: scorable,
            UnscorableDirectionCount: labels.Count - scorable,
            Labels: labels.AsReadOnly(),
            Distribution: distribution,
            BoardCount: bench.Boards.Count,
            M6BoardCount: m6Boards.Count,
            M6Share: bench.Boards.Count == 0 ? 0.0 : m6Boards.Count / (double)bench.Boards.Count,
            M6Boards: m6Boards.AsReadOnly());
    }

    /// <summary>
    /// L'étiquette d'une direction, et rien d'autre : ni <c>M5</c> (non détectable
    /// automatiquement — le harnais n'embarque aucune ressource de fréquence lexicale, et un
    /// proxy inventé donnerait une fausse impression de rigueur), ni <c>M6</c> (unité board).
    /// </summary>
    internal static (string Mode, double RBar, int ScoredCount) LabelDirection(
        IReadOnlyList<string> referenceWords, IReadOnlyList<ClueDecodeLine> decodes)
    {
        var scored = decodes
            .Where(d => d.DecodeFailureKind is null && d.R is not null && d.Picked is not null)
            .ToList();

        // D6 : ni indice valide, ni décodage exploitable. Ce n'est pas un mode d'échec
        // sémantique — le rapport le comptera à part.
        if (scored.Count == 0)
            return (FailureModes.Unclassified, 0.0, 0);

        var rBar = scored.Average(d => d.R!.Value);
        if (rBar >= SuccessThreshold)
            return (FailureModes.M0, rBar, scored.Count);

        // M2 — « Hôpital » : ≥ 2 décodages à R = 0,5 ET LA MÊME FACE MANQUÉE à chaque fois.
        var halves = scored.Where(d => d.R!.Value == 0.5).ToList();
        if (halves.Count >= ConcentrationThreshold)
        {
            var missed = halves
                .Select(d => referenceWords.Single(w => !d.Picked!.Contains(w)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (missed.Count == 1)
                return (FailureModes.M2, rBar, scored.Count);
        }

        var wrongPerDecode = scored
            .Select(d => d.Picked!.Where(w => !referenceWords.Contains(w)).ToList())
            .ToList();
        var wrongCounts = wrongPerDecode
            .SelectMany(w => w)
            .GroupBy(w => w, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // M3 — collision : un même mot non-référence choisi dans ≥ 2 décodages (CONCENTRÉ).
        if (wrongCounts.Values.Any(c => c >= ConcentrationThreshold))
            return (FailureModes.M3, rBar, scored.Count);

        // M4 — relation trop indirecte : R̄ = 0 ET aucun mot commun aux paires choisies.
        // D5 : au moins deux décodages, sinon « aucun mot commun » est vide de sens.
        // D9 : AVANT M1 — sous l'ordre inverse, M4 serait inatteignable (cf. PriorityOrder).
        if (rBar == 0.0 && scored.Count >= 2)
        {
            var common = wrongPerDecode
                .Select(w => (IEnumerable<string>)w)
                .Aggregate((a, b) => a.Intersect(b, StringComparer.Ordinal))
                .ToList();

            if (common.Count == 0)
                return (FailureModes.M4, rBar, scored.Count);
        }

        // M1 — trop générique : R̄ ≤ 0,5 ET mots faux DISPERSÉS.
        if (rBar <= 0.5 && wrongCounts.Count >= DispersionThreshold)
            return (FailureModes.M1, rBar, scored.Count);

        return (FailureModes.Unclassified, rBar, scored.Count);
    }
}
