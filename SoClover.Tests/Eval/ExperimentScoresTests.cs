using SoClover.Eval.Langfuse;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ExperimentScoresTests
{
    private const string ExperimentId = "run.9a829dc206d2";

    private static IReadOnlyList<ItemSpans> Items() => OtlpExperimentBuilder.Build(new ExperimentContext(
        ExperimentId, "ds_1", "9a829dc206d2",
        LangfuseFixtures.Bench(), LangfuseFixtures.Run(), LangfuseFixtures.Decoded())).Items;

    [Fact]
    public void Un_score_r_par_decodage_exploitable_rattache_a_son_observation()
    {
        var items = Items();
        var top = items.Single(i => i.ItemId == "dev-001-Top");

        var rScores = ExperimentScores.ForItems(ExperimentId, items)
            .Where(s => s.Name == "r" && s.TraceId == top.TraceId)
            .ToList();

        Assert.Equal(new[] { 1.0, 0.5, 0.0 }, rScores.Select(s => s.Value));
        Assert.Equal(top.Decodes.Select(d => d.SpanId), rScores.Select(s => s.ObservationId));
    }

    [Fact]
    public void Un_echec_de_format_n_emet_pas_de_score_r_et_sort_de_la_moyenne()
    {
        var bottom = ExperimentScores.ForItems(ExperimentId, Items())
            .Where(s => s.TraceId == Items().Single(i => i.ItemId == "dev-001-Bottom").TraceId)
            .ToList();

        Assert.Equal(2, bottom.Count(s => s.Name == "r"));
        var recovery = bottom.Single(s => s.Name == "recovery");
        Assert.Equal(0.75, recovery.Value, precision: 10);
        Assert.Equal("2 décodage(s) exploitable(s) sur 3", recovery.Comment);
    }

    /// <summary>D6 : une direction sans décodage exploitable n'est pas un échec sémantique — aucun score, jamais 0.</summary>
    [Fact]
    public void Une_direction_sans_decodage_n_a_aucun_score()
    {
        var left = Items().Single(i => i.ItemId == "dev-001-Left");

        Assert.DoesNotContain(ExperimentScores.ForItems(ExperimentId, Items()), s => s.TraceId == left.TraceId);
    }

    [Fact]
    public void Le_recovery_d_item_se_pose_sur_la_racine()
    {
        var top = Items().Single(i => i.ItemId == "dev-001-Top");

        var recovery = ExperimentScores.ForItems(ExperimentId, Items())
            .Single(s => s.Name == "recovery" && s.TraceId == top.TraceId);

        Assert.Equal(top.RootSpanId, recovery.ObservationId);
        Assert.Equal(0.5, recovery.Value, precision: 10);
    }

    [Fact]
    public void Les_scores_de_run_portent_leur_effectif_et_le_dataset_run()
    {
        var scores = ExperimentScores.ForRun(ExperimentId, "run_1", LangfuseFixtures.Metrics());

        var recovery = scores.Single(s => s.Name == "recovery");
        Assert.Equal(0.370, recovery.Value);
        Assert.Equal("run_1", recovery.DatasetRunId);
        Assert.Equal("n = 154", recovery.Comment);
        Assert.Null(recovery.TraceId);
        Assert.Equal(
            new[] { "recovery", "half_rate", "valid_rate", "first_attempt_rate", "board_positions", "decode_failure_rate" },
            scores.Select(s => s.Name));
    }

    [Fact]
    public void Un_denominateur_nul_supprime_le_score_au_lieu_de_publier_zero()
    {
        var scores = ExperimentScores.ForRun(ExperimentId, "run_1",
            LangfuseFixtures.Metrics(MetricCounts.Zero with { DecodedItems = 0, ScoredBoards = 0, Decodes = 480 }));

        Assert.DoesNotContain(scores, s => s.Name is "recovery" or "half_rate" or "board_positions");
        Assert.Contains(scores, s => s.Name == "decode_failure_rate");
    }

    [Fact]
    public void Les_ids_de_score_sont_deterministes()
    {
        Assert.Equal(
            ExperimentScores.ForItems(ExperimentId, Items()).Select(s => s.Id),
            ExperimentScores.ForItems(ExperimentId, Items()).Select(s => s.Id));
        Assert.Equal(32, ExperimentScores.ScoreId(ExperimentId, "run", "recovery").Length);
    }
}
