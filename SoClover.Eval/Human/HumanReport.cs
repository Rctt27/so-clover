using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

public sealed record OutcomeCount(string Outcome, int Count);

public sealed record ElapsedByOutcome(
    string Outcome, int Count, double MedianSeconds, int MinSeconds, int MaxSeconds);

public sealed record FatigueBucket(int FromOrdinal, int ToOrdinal, double MeanElapsedSeconds, int Count);

public sealed record RelationCount(string RelationType, int Count);

public sealed record ElicitationReport(
    int ItemsSaved,
    int ItemsPlanned,
    IReadOnlyList<OutcomeCount> Outcomes,
    double PassRate,
    IReadOnlyList<ElapsedByOutcome> Elapsed,
    IReadOnlyList<FatigueBucket> Fatigue,
    IReadOnlyList<RelationCount> Relations,
    int AssistedClueCount,
    int RejectedClueCount);

public sealed record FamilyOutcome(
    string Family, string Label, int Count,
    double OptionAWinRate, double OptionBWinRate, double TieRate);

public sealed record ComparisonReport(
    int ItemsJudged,
    int ItemsPlanned,
    IReadOnlyList<FamilyOutcome> Families,
    double Position1WinRate,
    bool Position1Suspect,
    double TieRate,
    int DuplicatePairCount,
    double IntraJudgeAgreement,
    int AnchorCount,
    int AnchorCorrect,
    bool AnchorSuspect);

/// <summary>
/// Agrégats de fin de séance. <b>Aucun appel LLM.</b>
/// <para>
/// Ce rapport <b>ne calcule ni l'accord décodeur/humain ni Cohen's κ</b> : c'est P6, et les
/// publier ici reviendrait à franchir une porte en la décrivant.
/// </para>
/// </summary>
public static class HumanReport
{
    /// <summary>Au-delà de cet écart à 50 %, le lot est déclaré suspect.</summary>
    public const double Position1Tolerance = 0.10;

    public const int FatigueBucketSize = 10;

    /// <summary>Plus d'une ancre ratée = juge distrait, lot suspect.</summary>
    public const int MaxAnchorFailures = 1;

    public static ElicitationReport ForElicitation(ElicitationContents contents)
    {
        var lines = contents.Elicitations;

        var outcomes = Outcomes.All
            .Select(o => new OutcomeCount(o, lines.Count(l => l.Outcome == o)))
            .ToList()
            .AsReadOnly();

        var elapsed = Outcomes.All
            .Select(o => lines.Where(l => l.Outcome == o).Select(l => l.ElapsedSeconds).ToList())
            .Select((values, i) => new ElapsedByOutcome(
                Outcomes.All[i],
                values.Count,
                Median(values),
                values.Count == 0 ? 0 : values.Min(),
                values.Count == 0 ? 0 : values.Max()))
            .ToList()
            .AsReadOnly();

        // Dérive du temps au fil de l'itemOrdinal : rend la fatigue mesurable a posteriori
        // plutôt que supposée.
        var fatigue = lines
            .GroupBy(l => (l.ItemOrdinal - 1) / FatigueBucketSize)
            .OrderBy(g => g.Key)
            .Select(g => new FatigueBucket(
                g.Key * FatigueBucketSize + 1,
                (g.Key + 1) * FatigueBucketSize,
                g.Average(l => (double)l.ElapsedSeconds),
                g.Count()))
            .ToList()
            .AsReadOnly();

        var relations = lines
            .Where(l => l.RelationType is not null)
            .GroupBy(l => l.RelationType!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new RelationCount(g.Key, g.Count()))
            .ToList()
            .AsReadOnly();

        return new ElicitationReport(
            ItemsSaved: lines.Count,
            ItemsPlanned: contents.Manifest.TargetCount,
            Outcomes: outcomes,
            PassRate: Ratio(lines.Count(l => l.Outcome == Outcomes.Pass), lines.Count),
            Elapsed: elapsed,
            Fatigue: fatigue,
            Relations: relations,
            AssistedClueCount: contents.Assists.Count(a => a.AssistedClue is not null),
            RejectedClueCount: lines.Sum(l => l.RejectedAttempts.Count));
    }

    public static ComparisonReport ForComparisons(ComparisonContents contents)
    {
        // Un re-jugement ajoute une ligne : seul le dernier verdict d'un comparisonId compte.
        var latest = HumanFile.LatestByComparisonId(contents).Values.ToList();

        var families = latest
            .GroupBy(c => c.Family, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FamilyOutcome(
                Family: g.Key,
                Label: $"{g.First().OptionA.Source} vs {g.First().OptionB.Source}",
                Count: g.Count(),
                OptionAWinRate: Ratio(g.Count(c => c.Verdict == JudgeSession.VerdictA), g.Count()),
                OptionBWinRate: Ratio(g.Count(c => c.Verdict == JudgeSession.VerdictB), g.Count()),
                TieRate: Ratio(g.Count(c => c.Verdict == JudgeSession.VerdictTie), g.Count())))
            .ToList()
            .AsReadOnly();

        var decided = latest.Where(c => c.Verdict != JudgeSession.VerdictTie).ToList();
        var position1Wins = decided.Count(WonInPositionOne);
        var position1WinRate = Ratio(position1Wins, decided.Count);

        // Cohérence intra-juge : le plafond réaliste de l'accord attendu en P6. Exiger du
        // décodeur un accord supérieur à celui du juge avec lui-même n'aurait aucun sens.
        var byId = latest.ToDictionary(c => c.ComparisonId, StringComparer.Ordinal);
        var pairs = latest
            .Where(c => c.DuplicateOf is not null && byId.ContainsKey(c.DuplicateOf))
            .ToList();
        var agreeing = pairs.Count(c => byId[c.DuplicateOf!].Verdict == c.Verdict);

        var anchors = latest.Where(c => c.Family == ComparisonFamilies.Anchor).ToList();
        var anchorsCorrect = anchors.Count(AnchorPickedTheModel);

        return new ComparisonReport(
            ItemsJudged: latest.Count,
            ItemsPlanned: contents.Manifest.TargetCount,
            Families: families,
            Position1WinRate: position1WinRate,
            Position1Suspect: decided.Count > 0
                && Math.Abs(position1WinRate - 0.5) > Position1Tolerance,
            TieRate: Ratio(latest.Count - decided.Count, latest.Count),
            DuplicatePairCount: pairs.Count,
            IntraJudgeAgreement: Ratio(agreeing, pairs.Count),
            AnchorCount: anchors.Count,
            AnchorCorrect: anchorsCorrect,
            AnchorSuspect: anchors.Count - anchorsCorrect > MaxAnchorFailures);
    }

    /// <summary>
    /// Le gagnant était-il affiché en position 1 ? <c>A</c> l'était si l'ordre était <c>AB</c> ;
    /// <c>B</c> l'était si l'ordre était <c>BA</c>.
    /// </summary>
    private static bool WonInPositionOne(ComparisonLine line) =>
        (line.Verdict == JudgeSession.VerdictA && line.PresentedOrder == PresentedOrders.Ab)
        || (line.Verdict == JudgeSession.VerdictB && line.PresentedOrder == PresentedOrders.Ba);

    private static bool AnchorPickedTheModel(ComparisonLine line)
    {
        var winner = line.Verdict switch
        {
            JudgeSession.VerdictA => line.OptionA,
            JudgeSession.VerdictB => line.OptionB,
            _ => null,
        };
        return winner?.Source == ComparisonSources.Model;
    }

    private static double Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return 0.0;

        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0.0 : numerator / (double)denominator;
}
