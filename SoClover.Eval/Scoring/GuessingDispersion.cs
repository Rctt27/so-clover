using SoClover.Eval.Decoder;
using SoClover.Eval.Human;

namespace SoClover.Eval.Scoring;

public sealed record GuessingDispersionResult(
    int PairedDirectionCount,
    double H1Recovery,
    double H2Recovery,
    double DecoderRecovery,
    double DeltaH1H2,
    double CiLowH1H2,
    double CiHighH1H2,
    double DeltaH1Decoder,
    double CiLowH1Decoder,
    double CiHighH1Decoder,
    double DeltaH2Decoder,
    double CiLowH2Decoder,
    double CiHighH2Decoder,
    bool CriterionMet,
    string Verdict,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Séance E : l'écart entre le décodeur et un humain devineur excède-t-il l'écart entre <b>deux</b>
/// humains devineurs ? Le critère est <b>relatif et sans nombre arbitraire</b> —
/// <c>|Δ(H1,D)| ≤ |Δ(H1,H2)|</c> — et remplace la porte d'accord à 0,75 par une mesure, ce que la
/// garde 6 exige au lieu d'un abaissement de seuil.
/// <para>
/// Les trois issues sont <b>pré-enregistrées au registre le 2026-08-08</b>, avant que H2 n'ait
/// deviné. Rien n'est décidé ici : le code applique une règle écrite d'avance. Les trois Δ portent
/// sur le <b>même</b> sous-ensemble de directions — celles communes à H1, H2 et au décodage — sans
/// quoi deux Δ de dénominateurs différents ne se compareraient pas.
/// </para>
/// </summary>
public static class GuessingDispersion
{
    public const string VerdictDansLaDispersion =
        "le décodeur tombe dans la dispersion humaine";
    public const string VerdictHorsDispersion =
        "le décodeur s'écarte de H1 plus que H2 ne s'en écarte";
    public const string VerdictNeTranchePas =
        "ne tranche pas — un troisième devineur est requis";

    /// <summary>
    /// Marge d'arrondi sur la comparaison des deux valeurs absolues. Le critère est une règle
    /// éditoriale (« tombe dans la dispersion »), pas une frontière numérique : l'égalité stricte
    /// de deux moyennes de doubles ne doit pas basculer le verdict sur un 1e-17.
    /// </summary>
    private const double ThresholdTolerance = 1e-9;

    public static GuessingDispersionResult Compare(
        GuessingContents h1,
        GuessingContents h2,
        DecodeContents decoded,
        int iterations = PairedComparison.DefaultBootstrapIterations,
        long seed = PairedComparison.DefaultBootstrapSeed)
    {
        RequireSameMontage(h1.Manifest, h2.Manifest);
        RequireDistinctSessions(h1, h2);

        // Même règle que la séance D : un décodage sans r est un échec de format, pas un échec
        // sémantique — il sort de la moyenne au lieu d'y entrer comme un zéro, et une direction
        // dont aucun décodage n'est exploitable sort de l'appariement tout entier.
        var decoderRBar = decoded.ClueDecodes
            .Where(d => d.R.HasValue)
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.Average(d => d.R!.Value));

        var byDirectionH2 = h2.Guesses.ToDictionary(g => (g.BoardId, g.Direction), g => g.R);

        var paired = h1.Guesses
            .Where(g => byDirectionH2.ContainsKey((g.BoardId, g.Direction)))
            .Where(g => decoderRBar.ContainsKey((g.BoardId, g.Direction)))
            .OrderBy(g => g.BoardId, StringComparer.Ordinal)
            .ThenBy(g => g.Direction, StringComparer.Ordinal)
            .ToList();

        if (paired.Count == 0)
            throw new InvalidOperationException(
                "Aucune direction commune aux deux séances et au décodage : rien à comparer.");

        var rH1 = paired.Select(g => g.R).ToList();
        var rH2 = paired.Select(g => byDirectionH2[(g.BoardId, g.Direction)]).ToList();
        var rD = paired.Select(g => decoderRBar[(g.BoardId, g.Direction)]).ToList();

        var (deltaH1H2, ciLowH1H2, ciHighH1H2) = Paired(rH1, rH2, iterations, seed);
        var (deltaH1D, ciLowH1D, ciHighH1D) = Paired(rH1, rD, iterations, seed);
        var (deltaH2D, ciLowH2D, ciHighH2D) = Paired(rH2, rD, iterations, seed);

        var criterionMet = Math.Abs(deltaH1D) <= Math.Abs(deltaH1H2) + ThresholdTolerance;
        var dispersionEstablished = ciLowH1H2 > 0 || ciHighH1H2 < 0;

        var reasons = new List<string>();
        string verdict;

        if (!dispersionEstablished)
        {
            // Deux humains dont l'écart n'est pas établi ne fournissent aucune échelle : comparer
            // |Δ(H1,D)| à |Δ(H1,H2)| revient alors à comparer deux points de bruit.
            verdict = VerdictNeTranchePas;
            reasons.Add(
                "l'IC à 95 % de Δ(H1,H2) contient zéro : la dispersion entre deux humains n'est " +
                "pas établie, il n'y a donc pas d'échelle à laquelle rapporter Δ(H1,D).");
            reasons.Add(
                $"le critère littéral |Δ(H1,D)| ≤ |Δ(H1,H2)| est {(criterionMet ? "vérifié" : "en défaut")} " +
                $"({Math.Abs(deltaH1D):0.000} contre {Math.Abs(deltaH1H2):0.000}), mais sur deux " +
                "quantités de cette précision cette lecture ne conclut pas.");
            reasons.Add(
                "issue déclarée d'avance au pré-enregistrement : un troisième devineur est requis.");
        }
        else if (criterionMet)
        {
            verdict = VerdictDansLaDispersion;
            reasons.Add(
                $"|Δ(H1,D)| = {Math.Abs(deltaH1D):0.000} ≤ |Δ(H1,H2)| = {Math.Abs(deltaH1H2):0.000} : " +
                "le décodeur s'écarte de H1 moins qu'un second humain ne s'en écarte.");
            reasons.Add(
                "Absence de preuve d'écart, jamais preuve d'équivalence — à n = 42 chaque Δ porte " +
                "±0,06, la lecture reste grossière (puissance déclarée au pré-enregistrement).");
        }
        else
        {
            verdict = VerdictHorsDispersion;
            reasons.Add(
                $"|Δ(H1,D)| = {Math.Abs(deltaH1D):0.000} > |Δ(H1,H2)| = {Math.Abs(deltaH1H2):0.000} : " +
                "le décodeur sort de la dispersion humaine mesurée.");
            reasons.Add(
                "Aucune interprétation n'est pré-autorisée au-delà de ce constat — la porte de " +
                "devinette ne peut pas être refondée sur cette base.");
        }

        return new GuessingDispersionResult(
            PairedDirectionCount: paired.Count,
            H1Recovery: rH1.Average(),
            H2Recovery: rH2.Average(),
            DecoderRecovery: rD.Average(),
            DeltaH1H2: deltaH1H2,
            CiLowH1H2: ciLowH1H2,
            CiHighH1H2: ciHighH1H2,
            DeltaH1Decoder: deltaH1D,
            CiLowH1Decoder: ciLowH1D,
            CiHighH1Decoder: ciHighH1D,
            DeltaH2Decoder: deltaH2D,
            CiLowH2Decoder: ciLowH2D,
            CiHighH2Decoder: ciHighH2D,
            CriterionMet: criterionMet,
            Verdict: verdict,
            Reasons: reasons.AsReadOnly());
    }

    private static (double Delta, double CiLow, double CiHigh) Paired(
        IReadOnlyList<double> left, IReadOnlyList<double> right, int iterations, long seed)
    {
        var deltas = left.Zip(right, (a, b) => a - b).ToList();
        var (ciLow, ciHigh) = iterations < 1
            ? (deltas.Min(), deltas.Max())
            : Bootstrap.Ci(deltas, static xs => xs.Average(), iterations, seed);
        return (deltas.Average(), ciLow, ciHigh);
    }

    /// <summary>
    /// « Reproduire la séance D à l'identique, une seule variable : la personne. » Un banc, une
    /// graine ou un run différents changeraient les indices ou l'ordre de présentation — la
    /// comparaison porterait alors sur deux choses à la fois, ce que la garde 1 interdit.
    /// </summary>
    private static void RequireSameMontage(GuessingManifest a, GuessingManifest b)
    {
        if (!string.Equals(a.BenchHash, b.BenchHash, StringComparison.Ordinal))
            throw new MismatchedBenchException(
                $"Bancs différents : {a.BenchHash} contre {b.BenchHash}. Les deux séances doivent " +
                "porter sur le même banc.");

        if (a.Seed != b.Seed)
            throw new MismatchedBenchException(
                $"Graines différentes : {a.Seed} contre {b.Seed}. Le plan de la séance E doit être " +
                "celui de la séance D, sans quoi les directions et l'ordre de présentation divergent.");

        if (!string.Equals(a.RunId, b.RunId, StringComparison.Ordinal))
            throw new MismatchedBenchException(
                $"Runs générateurs différents : {a.RunId} contre {b.RunId}. Les deux devineurs " +
                "doivent avoir vu les mêmes indices.");
    }

    /// <summary>
    /// Garde contre le piège nommé au pré-enregistrement : <c>--out</c> oublié, et l'on compare la
    /// séance D avec elle-même. Deux séances qui partagent un identifiant de session sont la même.
    /// </summary>
    private static void RequireDistinctSessions(GuessingContents h1, GuessingContents h2)
    {
        var shared = h1.Guesses.Select(g => g.SessionId)
            .Intersect(h2.Guesses.Select(g => g.SessionId), StringComparer.Ordinal)
            .ToList();

        if (shared.Count > 0)
            throw new InvalidOperationException(
                $"Les deux fichiers partagent la session « {shared[0]} » : c'est la même séance, " +
                "pas deux devineurs. Vérifier que la séance E a bien été lancée avec --out.");
    }
}
