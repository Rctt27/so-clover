using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// <c>RunMetrics.Compute</c> documente que le sous-ensemble sort aussi les tentatives hors
/// périmètre des dénominateurs de santé — « sinon <c>parse_failure_rate</c> et
/// <c>decode_failure_rate</c> parleraient d'un banc que le rapport ne prétend plus couvrir ».
/// </summary>
public class SubsetHealthDenominatorTests
{
    // Deux boards = 8 directions ; le sous-ensemble n'en retient que 4, dont la seule tentative
    // illisible. Numérateur restreint et dénominateur entier donneraient 1/8 au lieu de 1/4 :
    // un taux d'échec de parsing divisé par deux, et parfaitement plausible.
    [Fact]
    public void Le_taux_d_echec_de_parsing_se_rapporte_aux_seules_tentatives_du_sous_ensemble()
    {
        var bench = HumanTestData.Bench(boardCount: 2);
        var full = HumanTestData.Run(bench, "run-a", "modelA");

        var attempts = full.Attempts
            .Select(a => a.BoardId == "dev-000" && a.Direction == "Top"
                ? a with { Valid = false, FailureKind = "unparseable" }
                : a)
            .ToList();
        var run = new RunContents(full.Manifest, attempts.AsReadOnly());

        var subset = new HashSet<(string BoardId, string Direction)>
        {
            ("dev-000", "Top"), ("dev-000", "Right"), ("dev-000", "Bottom"), ("dev-000", "Left"),
        };

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 1, subset);

        Assert.Equal(0.25, metrics.ParseFailureRate);
    }

    [Fact]
    public void Sans_sous_ensemble_le_denominateur_reste_toutes_les_tentatives()
    {
        var bench = HumanTestData.Bench(boardCount: 2);
        var full = HumanTestData.Run(bench, "run-a", "modelA");

        var attempts = full.Attempts
            .Select(a => a.BoardId == "dev-000" && a.Direction == "Top"
                ? a with { Valid = false, FailureKind = "unparseable" }
                : a)
            .ToList();
        var run = new RunContents(full.Manifest, attempts.AsReadOnly());

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 1);

        Assert.Equal(0.125, metrics.ParseFailureRate);
    }
}
