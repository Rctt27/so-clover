using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Un <c>.metrics.json</c> vit à un chemin fixe par run (<c>ScoreCommand</c> écrit dans
/// <c>Path.ChangeExtension(runPath, ".metrics.json")</c>) : chaque <c>score</c> écrase le
/// précédent. Sans provenance inscrite, le même fichier signifie tantôt « plafond joué »
/// tantôt « plafond sur les seuls solide » selon la dernière commande lancée — et
/// <c>calibrate --saturation-metrics</c> lirait la mauvaise valeur en silence, sur une porte
/// définie sur les indices <c>solide</c>.
/// </summary>
public class MetricsSubsetProvenanceTests
{
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);

    private MetricsReport ComputeWithSubset(string? outcomeFilter)
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, plan);
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);
        var run = new SoClover.Eval.Runner.RunContents(manifest, attempts);

        var outcomes = SubsetSelector.ParseOutcomes(outcomeFilter);
        var subset = SubsetSelector.FromElicitation(elicitation, outcomes);

        return RunMetrics.Compute(
            _bench, run, decoded: null, maxAttempts: 1, subset,
            subsetFile: "elicitation.dev.jsonl", subsetOutcome: outcomeFilter);
    }

    [Fact]
    public void Le_rapport_inscrit_le_fichier_et_le_filtre_d_issues_du_sous_ensemble()
    {
        var metrics = ComputeWithSubset("solide");

        Assert.Equal("elicitation.dev.jsonl", metrics.SubsetFile);
        Assert.Equal("solide", metrics.SubsetOutcome);
    }

    [Fact]
    public void Sans_sous_ensemble_la_provenance_reste_nulle()
    {
        var run = HumanTestData.Run(_bench, "run-a", "modelA");

        var metrics = RunMetrics.Compute(_bench, run, decoded: null, maxAttempts: 1);

        Assert.Null(metrics.SubsetFile);
        Assert.Null(metrics.SubsetOutcome);
    }

    // Le fichier est le seul support de la provenance : si elle ne survit pas à l'aller-retour
    // JSON, calibrate ne peut rien vérifier.
    [Fact]
    public void La_provenance_survit_a_l_aller_retour_json()
    {
        var metrics = ComputeWithSubset("solide");

        var round = EvalJson.Deserialize<MetricsReport>(EvalJson.Serialize(metrics));

        Assert.Equal("elicitation.dev.jsonl", round.SubsetFile);
        Assert.Equal("solide", round.SubsetOutcome);
    }
}
