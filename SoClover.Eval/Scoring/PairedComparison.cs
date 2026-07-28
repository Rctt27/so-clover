using SoClover.Eval.Bench;

namespace SoClover.Eval.Scoring;

/// <summary>Comparer deux runs de bancs différents est une erreur de protocole, pas une approximation.</summary>
public sealed class MismatchedBenchException : InvalidOperationException
{
    public MismatchedBenchException(string message) : base(message) { }
}

public sealed record PairedComparisonResult(
    int PairedItemCount,
    double DeltaRecovery,
    double CiLow,
    double CiHigh,
    double BaselineRecovery,
    double VariantRecovery,
    double DeltaValidRate,
    double DeltaBoardSolved,
    string Verdict,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Comparaison <b>appariée</b> de deux runs sur le même banc : delta par item, Δ<c>recovery</c>
/// moyen avec intervalle de confiance bootstrap apparié, et verdict de la règle de promotion.
/// <para>
/// L'appariement élimine la variance inter-items et ramène la différence détectable de ~8 points
/// (non apparié, 240 items) à ~4-5 points. Sans l'intervalle de confiance, « +3 points » ne se
/// distingue pas du bruit.
/// </para>
/// </summary>
public static class PairedComparison
{
    public const double PromotionRecoveryGain = 0.03;
    public const double MaxValidRateLoss = 0.01;
    public const double MaxBoardSolvedRegression = 0.05;
    public const int DefaultBootstrapIterations = 10_000;
    public const long DefaultBootstrapSeed = 20260727777;

    /// <summary>
    /// Marge d'arrondi sur les comparaisons de seuil. Les taux sont des différences de doubles :
    /// une perte de valid_rate « d'exactement 1 point » vaut en réalité -0,010000000000000009 et
    /// franchirait un seuil comparé strictement. Le seuil est une règle éditoriale exprimée en
    /// points, pas une frontière numérique — l'égalité doit rester acceptable.
    /// </summary>
    private const double ThresholdTolerance = 1e-9;

    public static PairedComparisonResult Compare(
        MetricsReport baseline,
        MetricsReport variant,
        int bootstrapIterations = DefaultBootstrapIterations,
        long seed = DefaultBootstrapSeed)
    {
        if (baseline.BenchHash != variant.BenchHash)
            throw new MismatchedBenchException(
                $"Comparaison impossible : benchHash {baseline.BenchHash} vs {variant.BenchHash}. " +
                "Deux variantes se comparent toujours sur les mêmes items, appariés.");

        var pairedKeys = baseline.PerItemRBar.Keys
            .Where(variant.PerItemRBar.ContainsKey)
            .OrderBy(k => k.BoardId, StringComparer.Ordinal)
            .ThenBy(k => k.Direction, StringComparer.Ordinal)
            .ToList();

        if (pairedKeys.Count == 0)
            throw new InvalidOperationException(
                "Aucun item commun aux deux runs : rien à comparer.");

        var deltas = pairedKeys
            .Select(k => variant.PerItemRBar[k] - baseline.PerItemRBar[k])
            .ToList();

        var deltaRecovery = deltas.Average();
        var (ciLow, ciHigh) = BootstrapCi(deltas, bootstrapIterations, seed);

        var deltaValidRate = variant.ValidRate - baseline.ValidRate;
        var deltaBoardSolved = variant.BoardSolved - baseline.BoardSolved;

        var reasons = new List<string>();
        var disqualified = false;

        if (deltaValidRate < -MaxValidRateLoss - ThresholdTolerance)
        {
            reasons.Add($"valid_rate perd {(-deltaValidRate) * 100:0.0} pts (> 1 pt toléré)");
            disqualified = true;
        }
        if (-deltaBoardSolved > MaxBoardSolvedRegression + ThresholdTolerance)
        {
            reasons.Add($"board_solved régresse de {(-deltaBoardSolved) * 100:0.0} pts (> 5 pts tolérés)");
            disqualified = true;
        }
        if (deltaRecovery <= -PromotionRecoveryGain + ThresholdTolerance)
        {
            reasons.Add($"recovery perd {(-deltaRecovery) * 100:0.0} pts");
            disqualified = true;
        }

        string verdict;
        if (disqualified)
        {
            verdict = "écarté";
        }
        else if (deltaRecovery >= PromotionRecoveryGain - ThresholdTolerance)
        {
            verdict = "retenu";
            reasons.Add($"recovery gagne {deltaRecovery * 100:0.0} pts (≥ 3 pts requis)");
        }
        else
        {
            verdict = "neutre";
            reasons.Add($"recovery varie de {deltaRecovery * 100:+0.0;-0.0;0.0} pts, sous le seuil de 3 pts");
        }

        if (ciLow <= 0 && ciHigh >= 0)
            reasons.Add("l'IC à 95 % contient zéro : le delta n'est pas distinguable du bruit");

        return new PairedComparisonResult(
            PairedItemCount: pairedKeys.Count,
            DeltaRecovery: deltaRecovery,
            CiLow: ciLow,
            CiHigh: ciHigh,
            BaselineRecovery: baseline.Recovery,
            VariantRecovery: variant.Recovery,
            DeltaValidRate: deltaValidRate,
            DeltaBoardSolved: deltaBoardSolved,
            Verdict: verdict,
            Reasons: reasons.AsReadOnly());
    }

    /// <summary>
    /// Bootstrap apparié : rééchantillonnage des <b>items</b> avec remise, percentiles 2,5 % et
    /// 97,5 % des moyennes obtenues. Déterministe à seed fixé.
    /// </summary>
    private static (double Low, double High) BootstrapCi(
        IReadOnlyList<double> deltas, int iterations, long seed)
    {
        if (iterations < 1)
            return (deltas.Min(), deltas.Max());

        var rng = new Xoshiro256SS(seed);
        var means = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var sum = 0.0;
            for (var j = 0; j < deltas.Count; j++)
                sum += deltas[rng.NextInt(deltas.Count)];
            means[i] = sum / deltas.Count;
        }

        Array.Sort(means);
        return (Percentile(means, 0.025), Percentile(means, 0.975));
    }

    private static double Percentile(double[] sorted, double p)
    {
        var index = (int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }
}
