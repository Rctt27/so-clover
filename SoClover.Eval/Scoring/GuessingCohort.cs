using SoClover.Eval.Decoder;
using SoClover.Eval.Human;

namespace SoClover.Eval.Scoring;

/// <summary>R̄ d'un agent sur le sous-ensemble commun.</summary>
public sealed record CohortRecovery(string SessionId, bool ViaKit, double Recovery);

/// <summary>
/// Écart apparié entre deux agents. <see cref="Separated"/> vaut vrai quand l'IC — au niveau
/// <b>corrigé</b> quand il s'agit d'une paire humaine — exclut zéro.
/// </summary>
public sealed record CohortGap(
    string LeftSessionId,
    string RightSessionId,
    double Delta,
    double CiLow,
    double CiHigh,
    bool Separated);

public sealed record GuessingCohortResult(
    int HumanCount,
    int PairedDirectionCount,
    IReadOnlyList<CohortRecovery> Humans,
    double DecoderRecovery,
    IReadOnlyList<CohortGap> HumanPairs,
    IReadOnlyList<CohortGap> DecoderGaps,
    double HumanDispersion,
    double DecoderDistance,
    double? KitOnlyDispersion,
    int KitOnlyPairCount,
    double CorrectedAlpha,
    bool DispersionEstablished,
    bool CriterionMet,
    string Verdict,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Règle d'agrégation pour <b>K devineurs humains</b>, K ≥ 2, <b>pré-enregistrée au registre le
/// 2026-08-09 avant le premier import</b>. Ces classes la figent, elles ne la choisissent pas.
/// <para>
/// <b>S</b> = moyenne des |Δ| sur les K(K−1)/2 paires humaines, l'échelle. <b>E</b> = moyenne des
/// |Δ| sur les K écarts humain↔décodeur, la quantité. Critère : <c>E ≤ S</c>.
/// </para>
/// <para>
/// Des <b>moyennes</b> et non des maxima : un maximum croîtrait mécaniquement avec K et ferait de
/// « plus de devineurs » un moyen de <i>passer</i> le critère — garde 7, un corpus plus grand sert
/// à mesurer, pas à réussir.
/// </para>
/// </summary>
public static class GuessingCohort
{
    public const string VerdictDansLaDispersion =
        "le décodeur tombe dans la dispersion humaine";
    public const string VerdictHorsDispersion =
        "le décodeur s'écarte des humains plus qu'ils ne s'écartent entre eux";
    public const string VerdictNeTranchePas =
        "ne tranche pas — la dispersion humaine n'est pas établie";

    /// <summary>
    /// Préfixe des séances arrivées par kit hors ligne (<c>GuessKitPage</c> le tire au chargement),
    /// contre <c>s-</c> pour une séance servie (<c>Guess</c> dans <c>Program.cs</c>). C'est le seul
    /// marqueur de transport que porte un corpus, et il est posé par construction des deux côtés.
    /// </summary>
    public const string KitSessionPrefix = "e-";

    private const double ThresholdTolerance = 1e-9;

    public static GuessingCohortResult Compare(
        IReadOnlyList<GuessingContents> humans,
        DecodeContents decoded,
        int iterations = PairedComparison.DefaultBootstrapIterations,
        long seed = PairedComparison.DefaultBootstrapSeed)
    {
        if (humans.Count < 2)
            throw new InvalidOperationException(
                $"La dispersion demande au moins deux devineurs, {humans.Count} fourni(s).");

        RequireSameMontage(humans);
        RequireDistinctSessions(humans);

        // Même règle que les séances D et E : un décodage sans r est un échec de format, pas un
        // échec sémantique.
        var decoderRBar = decoded.ClueDecodes
            .Where(d => d.R.HasValue)
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.Average(d => d.R!.Value));

        var perHuman = humans
            .Select(h => h.Guesses.ToDictionary(g => (g.BoardId, g.Direction), g => g.R))
            .ToList();

        // L'intersection, et rien d'autre : un seul dénominateur pour tous les Δ, sans quoi deux
        // écarts ne se compareraient pas. Elle ne peut que rétrécir quand K croît — c'est voulu.
        var keys = perHuman[0].Keys
            .Where(k => perHuman.All(h => h.ContainsKey(k)))
            .Where(decoderRBar.ContainsKey)
            .OrderBy(k => k.BoardId, StringComparer.Ordinal)
            .ThenBy(k => k.Direction, StringComparer.Ordinal)
            .ToList();

        if (keys.Count == 0)
            throw new InvalidOperationException(
                "Aucune direction commune à tous les corpus et au décodage : rien à comparer.");

        var series = perHuman.Select(h => keys.Select(k => h[k]).ToList()).ToList();
        var decoderSeries = keys.Select(k => decoderRBar[k]).ToList();
        var sessions = humans.Select(SessionOf).ToList();

        // Bonferroni sur les m paires : retenir la paire la plus séparée puis la tester EST une
        // sélection, et c'est sa correction — choisie d'avance, donc inattaquable, et conservatrice.
        var pairCount = humans.Count * (humans.Count - 1) / 2;
        var correctedAlpha = Bootstrap.DefaultAlpha / pairCount;

        var humanPairs = new List<CohortGap>(pairCount);
        for (var i = 0; i < humans.Count; i++)
        {
            for (var j = i + 1; j < humans.Count; j++)
            {
                var (delta, low, high) = Paired(series[i], series[j], iterations, seed, correctedAlpha);
                humanPairs.Add(new CohortGap(
                    sessions[i], sessions[j], delta, low, high, Separated: low > 0 || high < 0));
            }
        }

        var decoderGaps = new List<CohortGap>(humans.Count);
        for (var i = 0; i < humans.Count; i++)
        {
            var (delta, low, high) = Paired(
                series[i], decoderSeries, iterations, seed, Bootstrap.DefaultAlpha);
            decoderGaps.Add(new CohortGap(
                sessions[i], "décodeur", delta, low, high, Separated: low > 0 || high < 0));
        }

        var s = humanPairs.Average(p => Math.Abs(p.Delta));
        var e = decoderGaps.Average(g => Math.Abs(g.Delta));

        // Diagnostic déclaré, JAMAIS décisionnel : les paires kit ↔ kit mesurent un écart
        // personne-à-personne pur, là où toute paire impliquant la séance servie mêle la personne
        // et le transport.
        var kitOnly = humanPairs
            .Where(p => p.LeftSessionId.StartsWith(KitSessionPrefix, StringComparison.Ordinal)
                     && p.RightSessionId.StartsWith(KitSessionPrefix, StringComparison.Ordinal))
            .ToList();

        var criterionMet = e <= s + ThresholdTolerance;
        var dispersionEstablished = humanPairs.Any(p => p.Separated);

        var reasons = new List<string>();
        string verdict;

        if (!dispersionEstablished)
        {
            verdict = VerdictNeTranchePas;
            reasons.Add(
                $"aucune des {pairCount} paires humaines n'a d'IC excluant zéro au niveau corrigé " +
                $"({correctedAlpha:0.####}) : il n'y a pas d'échelle à laquelle rapporter E.");
            reasons.Add(
                $"le critère littéral E ≤ S est {(criterionMet ? "vérifié" : "en défaut")} " +
                $"({e:0.000} contre {s:0.000}), mais sur deux quantités de cette précision cette " +
                "lecture ne conclut pas.");
            reasons.Add(
                $"issue déclarée d'avance : un devineur de plus est requis (K = {humans.Count} " +
                $"sur n = {keys.Count}).");
        }
        else if (criterionMet)
        {
            verdict = VerdictDansLaDispersion;
            reasons.Add(
                $"E = {e:0.000} ≤ S = {s:0.000} : le décodeur s'écarte des humains moins qu'ils ne " +
                "s'écartent entre eux.");
            reasons.Add(
                "Absence de preuve d'écart, jamais preuve d'équivalence.");
        }
        else
        {
            verdict = VerdictHorsDispersion;
            reasons.Add(
                $"E = {e:0.000} > S = {s:0.000} : le décodeur sort de la dispersion humaine mesurée.");
            reasons.Add(
                "Aucune interprétation n'est pré-autorisée au-delà de ce constat.");
        }

        if (kitOnly.Count > 0)
        {
            reasons.Add(
                $"diagnostic (non décisionnel) : S restreint aux {kitOnly.Count} paire(s) kit ↔ kit " +
                $"vaut {kitOnly.Average(p => Math.Abs(p.Delta)):0.000} — écart personne-à-personne " +
                "sans confusion avec le transport.");
        }

        return new GuessingCohortResult(
            HumanCount: humans.Count,
            PairedDirectionCount: keys.Count,
            Humans: humans.Select((h, i) => new CohortRecovery(
                sessions[i], ViaKit(sessions[i]), series[i].Average())).ToList().AsReadOnly(),
            DecoderRecovery: decoderSeries.Average(),
            HumanPairs: humanPairs.AsReadOnly(),
            DecoderGaps: decoderGaps.AsReadOnly(),
            HumanDispersion: s,
            DecoderDistance: e,
            KitOnlyDispersion: kitOnly.Count > 0 ? kitOnly.Average(p => Math.Abs(p.Delta)) : null,
            KitOnlyPairCount: kitOnly.Count,
            CorrectedAlpha: correctedAlpha,
            DispersionEstablished: dispersionEstablished,
            CriterionMet: criterionMet,
            Verdict: verdict,
            Reasons: reasons.AsReadOnly());
    }

    public static bool ViaKit(string sessionId) =>
        sessionId.StartsWith(KitSessionPrefix, StringComparison.Ordinal);

    private static string SessionOf(GuessingContents contents) =>
        contents.Guesses.Count > 0 ? contents.Guesses[0].SessionId : "(vide)";

    private static (double Delta, double CiLow, double CiHigh) Paired(
        IReadOnlyList<double> left, IReadOnlyList<double> right,
        int iterations, long seed, double alpha)
    {
        var deltas = left.Zip(right, (a, b) => a - b).ToList();
        var (ciLow, ciHigh) = iterations < 1
            ? (deltas.Min(), deltas.Max())
            : Bootstrap.Ci(deltas, static xs => xs.Average(), iterations, seed, alpha);
        return (deltas.Average(), ciLow, ciHigh);
    }

    /// <summary>
    /// La seule variable autorisée entre les séances est <i>la personne</i> — garde 1. La
    /// vérification est faite deux à deux sur l'ensemble, et non seulement contre le premier
    /// corpus : trois séances dont la troisième diverge doivent échouer même si les deux premières
    /// concordent.
    /// </summary>
    private static void RequireSameMontage(IReadOnlyList<GuessingContents> humans)
    {
        for (var i = 1; i < humans.Count; i++)
        {
            var a = humans[0].Manifest;
            var b = humans[i].Manifest;

            if (!string.Equals(a.BenchHash, b.BenchHash, StringComparison.Ordinal))
                throw new MismatchedBenchException(
                    $"Bancs différents : {a.BenchHash} contre {b.BenchHash}.");

            if (a.Seed != b.Seed)
                throw new MismatchedBenchException(
                    $"Graines différentes : {a.Seed} contre {b.Seed}.");

            if (!string.Equals(a.RunId, b.RunId, StringComparison.Ordinal))
                throw new MismatchedBenchException(
                    $"Runs générateurs différents : {a.RunId} contre {b.RunId}.");
        }
    }

    private static void RequireDistinctSessions(IReadOnlyList<GuessingContents> humans)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < humans.Count; i++)
        {
            foreach (var session in humans[i].Guesses.Select(g => g.SessionId).Distinct(StringComparer.Ordinal))
            {
                if (seen.TryGetValue(session, out var first))
                    throw new InvalidOperationException(
                        $"Les corpus {first + 1} et {i + 1} partagent la session « {session} » : " +
                        "c'est la même séance, pas deux devineurs.");

                seen[session] = i;
            }
        }
    }
}
