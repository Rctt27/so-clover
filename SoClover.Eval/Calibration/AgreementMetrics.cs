using SoClover.Eval.Human;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Calibration;

/// <summary>Un couple doublement tranché : c'est l'unité de l'accord, de κ et du bootstrap.</summary>
public sealed record VerdictPair(string Human, string Decoder);

public sealed record ContingencyCell(string HumanVerdict, string DecoderVerdict, int Count);

/// <summary>
/// <see cref="ScorableCoupleCount"/> ne compte que les couples <b>scorables</b> de la famille
/// (verdict décodeur défini) — pas le total, et pas les décidés. Trois compteurs de couples
/// différents vivent dans ce cycle (<see cref="AgreementReport.CoupleCount"/>,
/// <see cref="ScorableCoupleCount"/>, <c>CalibrationManifest.CoupleAndAnchorCount</c>) : le nom
/// porte la sémantique pour que les artefacts committés restent lisibles sans ce contexte.
/// </summary>
public sealed record FamilyAgreement(
    string Family, int ScorableCoupleCount, int DecidedCount, double Agreement, double Kappa);

/// <summary>
/// <see cref="CoupleCount"/> : <b>tous</b> les couples principaux du lot, ancres exclues,
/// non-scorables inclus (<c>lot.Couples.Count</c>) — pas le même compteur que
/// <see cref="FamilyAgreement.ScorableCoupleCount"/> ni que
/// <c>CalibrationManifest.CoupleAndAnchorCount</c>.
/// </summary>
public sealed record AgreementReport(
    int CoupleCount,
    int DecidedCount,
    int UnscorableCoupleCount,
    double Agreement,
    double AgreementCiLow,
    double AgreementCiHigh,
    double Kappa,
    double KappaCiLow,
    double KappaCiHigh,
    double Pabak,
    double HumanTieRate,
    double DecoderTieRate,
    IReadOnlyList<ContingencyCell> Contingency,
    double HumanMarginalA,
    double HumanMarginalB,
    double DecoderMarginalA,
    double DecoderMarginalB,
    IReadOnlyList<FamilyAgreement> ByFamily,
    int AnchorCount,
    int AnchorCorrect,
    bool ThinDenominator);

/// <summary>
/// Accord décodeur/humain et Cohen's κ, sur les <b>slots canoniques</b>.
/// <para>
/// <b>Jamais sur les positions.</b> C'est l'invariant central du design P4-P5 : <c>optionA</c> et
/// <c>optionB</c> sont ordonnés par source de façon déterministe, <c>presentedOrder</c> dit
/// seulement lequel fut affiché en position 1. Un κ calculé sur les positions mesurerait le biais
/// de position, pas l'accord — et aucun test fonctionnel ne s'en apercevrait.
/// <see cref="CalibrationCouple"/> ne porte volontairement aucun champ d'ordre de présentation.
/// </para>
/// </summary>
public static class AgreementMetrics
{
    /// <summary>Tout écart compte. Une valeur non nulle se décide AVANT de lire l'accord.</summary>
    public const double DefaultEpsilon = 0.0;

    public const long DefaultBootstrapSeed = 20260805001;

    /// <summary>Sous ce dénominateur, l'IC traverse la porte : le chiffre est publié marqué fragile.</summary>
    public const int ThinDenominatorThreshold = 40;

    /// <summary>
    /// <c>null</c> quand l'un des deux R̄ est indéfini : R̄ indéfini d'un côté ne se compare pas.
    /// Compter <c>tie</c> gonflerait le taux d'égalité du décodeur ; compter perdant serait faux.
    /// </summary>
    public static string? DecoderVerdict(double? rBarA, double? rBarB, double epsilon)
    {
        if (rBarA is not { } a || rBarB is not { } b)
            return null;

        if (a - b > epsilon) return JudgeSession.VerdictA;
        if (b - a > epsilon) return JudgeSession.VerdictB;
        return JudgeSession.VerdictTie;
    }

    public static AgreementReport Compute(
        CalibrationLot lot,
        IReadOnlyDictionary<CalibrationClue, double?> rBar,
        double epsilon,
        int bootstrapIterations,
        long seed)
    {
        double? Of(CalibrationCouple c, string clue) =>
            rBar.TryGetValue(new CalibrationClue(c.BoardId, c.Direction, clue), out var v) ? v : null;

        var scored = new List<(CalibrationCouple Couple, string Decoder)>();
        var unscorable = 0;

        foreach (var couple in lot.Couples)
        {
            var verdict = DecoderVerdict(Of(couple, couple.ClueA), Of(couple, couple.ClueB), epsilon);
            if (verdict is null) { unscorable++; continue; }
            scored.Add((couple, verdict));
        }

        // L'accord brut est calculé HORS ÉGALITÉS, comme le prescrit le PRD :
        //   accord = #(verdicts identiques) / #(couples où humain ET décodeur tranchent)
        var decided = scored
            .Where(s => s.Couple.HumanVerdict != JudgeSession.VerdictTie
                        && s.Decoder != JudgeSession.VerdictTie)
            .Select(s => new VerdictPair(s.Couple.HumanVerdict, s.Decoder))
            .ToList();

        var agreement = AgreementOf(decided);
        var kappa = KappaOf(decided);

        var (agreementLow, agreementHigh) = decided.Count == 0
            ? (0.0, 0.0)
            : Bootstrap.Ci(decided, AgreementOf, bootstrapIterations, seed);
        var (kappaLow, kappaHigh) = decided.Count == 0
            ? (0.0, 0.0)
            : Bootstrap.Ci(decided, KappaOf, bootstrapIterations, seed);

        var byFamily = scored
            .GroupBy(s => s.Couple.Family, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var pairs = g
                    .Where(s => s.Couple.HumanVerdict != JudgeSession.VerdictTie
                                && s.Decoder != JudgeSession.VerdictTie)
                    .Select(s => new VerdictPair(s.Couple.HumanVerdict, s.Decoder))
                    .ToList();
                return new FamilyAgreement(g.Key, g.Count(), pairs.Count, AgreementOf(pairs), KappaOf(pairs));
            })
            .ToList()
            .AsReadOnly();

        var categories = new[] { JudgeSession.VerdictA, JudgeSession.VerdictB };
        var contingency = categories
            .SelectMany(h => categories.Select(d =>
                new ContingencyCell(h, d, decided.Count(p => p.Human == h && p.Decoder == d))))
            .ToList()
            .AsReadOnly();

        var anchorCorrect = lot.Anchors.Count(a =>
        {
            var verdict = DecoderVerdict(Of(a, a.ClueA), Of(a, a.ClueB), epsilon);
            var winnerSource = verdict switch
            {
                JudgeSession.VerdictA => a.SourceA,
                JudgeSession.VerdictB => a.SourceB,
                _ => null,
            };
            return winnerSource == ComparisonSources.Model;
        });

        return new AgreementReport(
            CoupleCount: lot.Couples.Count,
            DecidedCount: decided.Count,
            UnscorableCoupleCount: unscorable,
            Agreement: agreement,
            AgreementCiLow: agreementLow,
            AgreementCiHigh: agreementHigh,
            Kappa: kappa,
            KappaCiLow: kappaLow,
            KappaCiHigh: kappaHigh,
            // PABAK est un DIAGNOSTIC. Le PRD nomme κ ; la porte reste κ. Publier PABAK comme
            // porte serait changer la règle en cours de partie.
            Pabak: 2 * agreement - 1,
            HumanTieRate: Ratio(
                scored.Count(s => s.Couple.HumanVerdict == JudgeSession.VerdictTie), scored.Count),
            DecoderTieRate: Ratio(
                scored.Count(s => s.Decoder == JudgeSession.VerdictTie), scored.Count),
            Contingency: contingency,
            HumanMarginalA: Ratio(decided.Count(p => p.Human == JudgeSession.VerdictA), decided.Count),
            HumanMarginalB: Ratio(decided.Count(p => p.Human == JudgeSession.VerdictB), decided.Count),
            DecoderMarginalA: Ratio(decided.Count(p => p.Decoder == JudgeSession.VerdictA), decided.Count),
            DecoderMarginalB: Ratio(decided.Count(p => p.Decoder == JudgeSession.VerdictB), decided.Count),
            ByFamily: byFamily,
            AnchorCount: lot.Anchors.Count,
            AnchorCorrect: anchorCorrect,
            ThinDenominator: decided.Count < ThinDenominatorThreshold);
    }

    internal static double AgreementOf(IReadOnlyList<VerdictPair> pairs) =>
        pairs.Count == 0 ? 0.0 : pairs.Count(p => p.Human == p.Decoder) / (double)pairs.Count;

    /// <summary>
    /// κ à <b>deux catégories</b> <c>A</c>/<c>B</c> :
    /// <c>p_e = Σ_c P_humain(c) · P_décodeur(c)</c>, <c>κ = (p_o − p_e) / (1 − p_e)</c>.
    /// <para>
    /// Quand les marginales sont déséquilibrées — et elles le seront : sur <c>humanVsModel</c>,
    /// l'humain gagnera probablement la grande majorité des couples —, <c>p_e</c> s'approche de
    /// <c>p_o</c> et <b>κ s'effondre malgré un accord élevé</b>. Ce n'est pas un défaut du
    /// décodeur, c'est une propriété connue de κ sur distribution asymétrique : d'où la table de
    /// contingence et les marginales, publiées à côté.
    /// </para>
    /// <para>
    /// Marginales parfaitement dégénérées (<c>1 − p_e = 0</c>) : κ vaut <b>0</b>, jamais 1. Rendre
    /// « accord parfait » sur une division indéfinie serait le pire des deux mondes.
    /// </para>
    /// </summary>
    internal static double KappaOf(IReadOnlyList<VerdictPair> pairs)
    {
        if (pairs.Count == 0) return 0.0;

        var n = (double)pairs.Count;
        var po = pairs.Count(p => p.Human == p.Decoder) / n;

        var pe = 0.0;
        foreach (var category in new[] { JudgeSession.VerdictA, JudgeSession.VerdictB })
            pe += pairs.Count(p => p.Human == category) / n * (pairs.Count(p => p.Decoder == category) / n);

        return Math.Abs(1 - pe) < 1e-12 ? 0.0 : (po - pe) / (1 - pe);
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0.0 : numerator / (double)denominator;
}
