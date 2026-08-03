using SoClover.Eval.Bench;

namespace SoClover.Eval.Scoring;

/// <summary>
/// Intervalle de confiance bootstrap, <b>partagé</b> par toutes les statistiques du harnais :
/// Δ<c>recovery</c> (moyenne des deltas appariés), accord décodeur/humain (proportion), Cohen's κ.
/// <para>
/// Un seul rééchantillonnage pour tout le monde, et pas trois : dupliquer un bootstrap est le
/// genre de dette qui produit ensuite deux IC légèrement différents pour la même raison, sans
/// que personne ne sache lequel croire.
/// </para>
/// <para>
/// Déterministe à seed fixé : <see cref="Xoshiro256SS"/> et jamais <c>System.Random</c>, dont la
/// séquence n'est pas garantie stable entre versions du runtime.
/// </para>
/// </summary>
public static class Bootstrap
{
    public const int DefaultIterations = 10_000;

    /// <summary>
    /// Rééchantillonne <paramref name="sample"/> avec remise, <paramref name="iterations"/> fois,
    /// applique <paramref name="statistic"/> à chaque rééchantillon, et rend les percentiles
    /// 2,5 % et 97,5 % des valeurs obtenues.
    /// <para>
    /// L'ordre des tirages reproduit celui de l'implémentation historique de
    /// <c>PairedComparison.BootstrapCi</c> — <c>sample.Count</c> appels à <c>NextInt</c> par
    /// itération, dans l'ordre — pour que les IC déjà publiés au registre restent reproductibles
    /// à l'identique après l'extraction.
    /// </para>
    /// </summary>
    public static (double Low, double High) Ci<T>(
        IReadOnlyList<T> sample,
        Func<IReadOnlyList<T>, double> statistic,
        int iterations,
        long seed)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(statistic);

        if (sample.Count == 0)
            throw new ArgumentException(
                "Un intervalle de confiance sur un échantillon vide n'a pas de sens.", nameof(sample));

        if (iterations < 1)
            throw new ArgumentOutOfRangeException(
                nameof(iterations), iterations, "Le nombre d'itérations doit être ≥ 1.");

        var rng = new Xoshiro256SS(seed);
        var values = new double[iterations];
        var resample = new T[sample.Count];

        for (var i = 0; i < iterations; i++)
        {
            for (var j = 0; j < sample.Count; j++)
                resample[j] = sample[rng.NextInt(sample.Count)];

            values[i] = statistic(resample);
        }

        Array.Sort(values);
        return (Percentile(values, 0.025), Percentile(values, 0.975));
    }

    internal static double Percentile(double[] sorted, double p)
    {
        var index = (int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }
}
