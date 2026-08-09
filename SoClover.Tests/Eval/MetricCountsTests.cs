using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Un taux sans ses effectifs se lit comme un fait alors qu'il peut n'être qu'un plancher
/// d'estimateur. <c>strict_2of2 = 0,000</c> sur 22 directions et sur 160 s'affichaient
/// identiquement, alors que la première valeur ne peut prendre que 0 ; 0,045 ; 0,091… — un zéro
/// y est le résultat attendu, pas un signal.
/// </summary>
public class MetricCountsTests
{
    private static RunContents RunWithOneParseFailure(SoClover.Eval.Bench.BenchContents bench)
    {
        var full = HumanTestData.Run(bench, "run-a", "modelA");
        var attempts = full.Attempts
            .Select(a => a.BoardId == "dev-000" && a.Direction == "Top"
                ? a with { Valid = false, FailureKind = "unparseable" }
                : a)
            .ToList();
        return new RunContents(full.Manifest, attempts.AsReadOnly());
    }

    [Fact]
    public void Le_rapport_porte_les_effectifs_derriere_les_taux_de_validite()
    {
        var bench = HumanTestData.Bench(boardCount: 2);
        var run = RunWithOneParseFailure(bench);

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 1);

        Assert.Equal(8, metrics.Counts.Attempts);
        Assert.Equal(1, metrics.Counts.ParseFailures);
        Assert.Equal(7, metrics.Counts.ValidItems);
    }

    [Fact]
    public void Les_effectifs_suivent_la_restriction_du_sous_ensemble()
    {
        var bench = HumanTestData.Bench(boardCount: 2);
        var run = RunWithOneParseFailure(bench);
        var subset = new HashSet<(string BoardId, string Direction)>
        {
            ("dev-000", "Top"), ("dev-000", "Right"), ("dev-000", "Bottom"), ("dev-000", "Left"),
        };

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 1, subset);

        Assert.Equal(4, metrics.Counts.Attempts);
        Assert.Equal(1, metrics.Counts.ParseFailures);
        Assert.Equal(3, metrics.Counts.ValidItems);
    }

    // Sans décodage, le dénominateur de recovery / strict_2of2 / half_rate est nul — et c'est
    // précisément ce que l'affichage doit rendre visible plutôt que d'imprimer 0,000.
    [Fact]
    public void Sans_decodage_le_denominateur_de_devinabilite_est_nul()
    {
        var bench = HumanTestData.Bench(boardCount: 2);
        var run = HumanTestData.Run(bench, "run-a", "modelA");

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 1);

        Assert.Equal(0, metrics.Counts.DecodedItems);
        Assert.Equal(0, metrics.Counts.StrictItems);
    }
}
