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

    /// <summary>Ruling 14 : comme dans RunMetrics.Compute, une direction sans indice valide compte R̄ = 0 (A-1).</summary>
    [Fact]
    public void Une_direction_sans_indice_valide_publie_un_recovery_nul_sur_la_racine()
    {
        var left = Items().Single(i => i.ItemId == "dev-001-Left");

        var scores = ExperimentScores.ForItems(ExperimentId, Items())
            .Where(s => s.TraceId == left.TraceId)
            .ToList();

        Assert.DoesNotContain(scores, s => s.Name == "r");
        var recovery = scores.Single(s => s.Name == "recovery");
        Assert.Equal(0.0, recovery.Value);
        Assert.Equal(left.RootSpanId, recovery.ObservationId);
        Assert.Equal("aucun indice valide (A-1), compté 0 comme dans RunMetrics", recovery.Comment);
    }

    /// <summary>Ruling 14 : un indice valide dont aucun décodage n'est exploitable compte R̄ = 0, comme dans RunMetrics.Compute.</summary>
    [Fact]
    public void Un_indice_valide_sans_decodage_exploitable_publie_un_recovery_nul()
    {
        var item = new ItemSpans("dev-001-Top", "trace", "root", [new DecodeSpan(0, "decode-0", null)], HasValidClue: true);

        var scores = ExperimentScores.ForItems(ExperimentId, [item]);

        Assert.DoesNotContain(scores, s => s.Name == "r");
        var recovery = scores.Single(s => s.Name == "recovery");
        Assert.Equal(0.0, recovery.Value);
        Assert.Equal("root", recovery.ObservationId);
        Assert.Equal("aucun décodage exploitable, compté 0 comme dans RunMetrics", recovery.Comment);
    }

    [Fact]
    public void La_moyenne_des_recovery_d_item_egale_le_recovery_de_RunMetrics()
    {
        var run = LangfuseFixtures.Run();
        var metrics = RunMetrics.Compute(
            LangfuseFixtures.Bench(), run, LangfuseFixtures.Decoded(), maxAttempts: run.Manifest.MaxRetries + 1);

        var itemRecoveries = ExperimentScores.ForItems(ExperimentId, Items())
            .Where(s => s.Name == "recovery")
            .Select(s => s.Value)
            .ToList();

        Assert.Equal(metrics.DirectionCount, itemRecoveries.Count);
        Assert.Equal(metrics.Recovery, itemRecoveries.Average(), precision: 10);
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
        Assert.Equal("n = 160", recovery.Comment);
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

        Assert.DoesNotContain(scores, s => s.Name is "half_rate" or "board_positions");
        Assert.Contains(scores, s => s.Name == "decode_failure_rate");

        // recovery porte sur DirectionCount (160), pas sur DecodedItems : 0 sur 160 directions
        // reste une mesure réelle même quand aucun item n'a été décodé.
        var recovery = scores.Single(s => s.Name == "recovery");
        Assert.Equal("n = 160", recovery.Comment);
    }

    [Fact]
    public void Un_banc_sans_direction_supprime_recovery_et_les_taux_par_direction()
    {
        var scores = ExperimentScores.ForRun(ExperimentId, "run_1",
            LangfuseFixtures.Metrics() with { DirectionCount = 0 });

        Assert.DoesNotContain(scores, s => s.Name is "recovery" or "valid_rate" or "first_attempt_rate");
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
