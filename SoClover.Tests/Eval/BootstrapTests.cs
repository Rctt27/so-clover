using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class BootstrapTests
{
    private static double Mean(IReadOnlyList<double> xs) => xs.Count == 0 ? 0.0 : xs.Average();

    [Fact]
    public void Is_deterministic_for_a_fixed_seed()
    {
        var sample = new[] { 0.0, 0.5, 1.0, 0.5, 0.0, 1.0 };

        var a = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);
        var b = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);

        Assert.Equal(a.Low, b.Low);
        Assert.Equal(a.High, b.High);
    }

    [Fact]
    public void A_different_seed_gives_a_different_interval()
    {
        var sample = new[] { 0.0, 0.5, 1.0, 0.5, 0.0, 1.0 };

        var a = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);
        // Seed 125, pas 100 : sur cet échantillon à 6 éléments/3 valeurs distinctes, la
        // distribution des moyennes rééchantillonnées n'a que 13 valeurs possibles — les
        // percentiles 2,5 %/97,5 % coïncident pour ~80 % des paires de graines arbitraires
        // (vérifié y compris contre l'implémentation historique de PairedComparison.BootstrapCi,
        // bit à bit identique). La paire (99, 100) coïncide ; (99, 125) diverge, de façon
        // parfaitement déterministe et reproductible — ce n'est pas un test flaky.
        var b = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 125);

        Assert.True(a.Low != b.Low || a.High != b.High);
    }

    [Fact]
    public void The_interval_brackets_the_statistic_of_the_original_sample()
    {
        var sample = Enumerable.Range(0, 60).Select(i => i % 2 == 0 ? 0.4 : 0.6).ToList();

        var (low, high) = Bootstrap.Ci(sample, Mean, iterations: 2_000, seed: 7);

        Assert.True(low <= 0.5);
        Assert.True(high >= 0.5);
    }

    // La statistique est un paramètre, pas une moyenne codée en dur : c'est toute la raison
    // de l'extraction — l'accord est une proportion, κ n'est ni l'une ni l'autre.
    [Fact]
    public void Accepts_a_proportion_statistic_and_brackets_it()
    {
        var sample = Enumerable.Range(0, 100).Select(i => i < 80).ToList();
        static double Share(IReadOnlyList<bool> xs) =>
            xs.Count == 0 ? 0.0 : xs.Count(x => x) / (double)xs.Count;

        var (low, high) = Bootstrap.Ci(sample, Share, iterations: 2_000, seed: 11);

        Assert.True(low <= 0.80 && 0.80 <= high);
        Assert.True(low > 0.60 && high < 0.95);
    }

    [Fact]
    public void A_constant_sample_yields_a_degenerate_interval()
    {
        var sample = Enumerable.Repeat(0.5, 30).ToList();

        var (low, high) = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 3);

        Assert.Equal(0.5, low, precision: 10);
        Assert.Equal(0.5, high, precision: 10);
    }

    [Fact]
    public void Refuses_an_empty_sample()
    {
        Assert.Throws<ArgumentException>(
            () => Bootstrap.Ci(Array.Empty<double>(), Mean, iterations: 10, seed: 1));
    }

    // Le repli « min/max » de PairedComparison reste chez lui : ici, moins d'une itération
    // est une erreur d'appel, pas un mode dégradé silencieux.
    [Fact]
    public void Refuses_fewer_than_one_iteration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Bootstrap.Ci(new[] { 0.5 }, Mean, iterations: 0, seed: 1));
    }
}
