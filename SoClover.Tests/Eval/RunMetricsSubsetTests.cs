using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class RunMetricsSubsetTests
{
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);

    [Fact]
    public void Sans_sous_ensemble_le_denominateur_reste_le_banc_entier()
    {
        var run = HumanTestData.Run(_bench, "run-a", "modelA");

        var metrics = RunMetrics.Compute(_bench, run, decoded: null, maxAttempts: 1);

        Assert.Equal(160, metrics.DirectionCount);
        Assert.Equal(40, metrics.BoardCount);
    }

    [Fact]
    public void Le_sous_ensemble_restreint_tous_les_denominateurs()
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, plan);
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);
        var run = new SoClover.Eval.Runner.RunContents(manifest, attempts);

        var subset = SubsetSelector.FromElicitation(elicitation, SubsetSelector.ParseOutcomes(null));
        var restricted = RunMetrics.Compute(_bench, run, decoded: null, maxAttempts: 1, subset);
        var unrestricted = RunMetrics.Compute(_bench, run, decoded: null, maxAttempts: 1);

        Assert.Equal(40, restricted.DirectionCount);
        Assert.Equal(160, unrestricted.DirectionCount);

        // valid_rate = 1 sur les 40 directions écrites, 0,25 rapporté au banc entier.
        Assert.Equal(1.0, restricted.ValidRate);
        Assert.Equal(0.25, unrestricted.ValidRate);
    }

    [Fact]
    public void Un_pass_compte_R_barre_egal_zero_dans_le_sous_ensemble()
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, plan, passEvery: 4);
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);
        var run = new SoClover.Eval.Runner.RunContents(manifest, attempts);

        var subset = SubsetSelector.FromElicitation(elicitation, SubsetSelector.ParseOutcomes(null));
        var metrics = RunMetrics.Compute(_bench, run, decoded: null, maxAttempts: 1, subset);

        Assert.Equal(40, metrics.DirectionCount);
        Assert.Equal(0.75, metrics.ValidRate, 6);

        // A-1 : les pass restent au dénominateur, ils ne sont pas retirés du banc.
        var resolved = SubsetSelector.FromElicitation(
            elicitation, SubsetSelector.ParseOutcomes("solide,tiede"));
        Assert.Equal(30, resolved.Count);
        Assert.Equal(1.0, RunMetrics.Compute(_bench, run, null, 1, resolved).ValidRate, 6);
    }

    [Fact]
    public void La_comparaison_appariee_nutilise_que_les_items_du_sous_ensemble()
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, plan);
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);
        var humanRun = new SoClover.Eval.Runner.RunContents(manifest, attempts);
        var modelRun = HumanTestData.Run(_bench, "run-a", "modelA");

        var subset = SubsetSelector.FromElicitation(elicitation, SubsetSelector.ParseOutcomes(null));
        var baseline = RunMetrics.Compute(_bench, modelRun, null, 1, subset);
        var variant = RunMetrics.Compute(_bench, humanRun, null, 1, subset);

        var result = PairedComparison.Compare(baseline, variant, bootstrapIterations: 100);

        Assert.Equal(40, result.PairedItemCount);
    }

    [Fact]
    public void La_cellule_reglages_du_registre_porte_la_mention_du_sous_ensemble()
    {
        var run = HumanTestData.Run(_bench, "run-a", "modelA");

        var without = ScoreCommand.ComposeSettings(run.Manifest, null, 160, 160);
        var with = ScoreCommand.ComposeSettings(run.Manifest, "elicitation.dev.jsonl", 40, 160);

        Assert.DoesNotContain("subset", without, StringComparison.Ordinal);
        Assert.Contains("subset=elicitation.dev.jsonl (40/160)", with, StringComparison.Ordinal);
    }
}
