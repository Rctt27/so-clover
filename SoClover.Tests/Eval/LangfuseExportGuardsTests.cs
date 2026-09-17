using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>Les refus de <c>langfuse-export</c> : mêmes règles que les verbes qu'il prolonge.</summary>
public class LangfuseExportGuardsTests
{
    private const string PlainMetrics = """{"runId":"x","subsetFile":null,"subsetOutcome":null}""";

    [Fact]
    public void Le_nom_d_experiment_accole_le_run_et_l_empreinte()
    {
        Assert.Equal("run-1.9a829dc206d2", LangfuseExportCommand.ExperimentName("run-1", "9a829dc206d2"));
    }

    [Fact]
    public void Un_run_decode_et_score_sur_le_banc_entier_est_exportable()
    {
        LangfuseExportCommand.RequireExportable(LangfuseFixtures.Run(), LangfuseFixtures.Decoded(), PlainMetrics);
    }

    [Fact]
    public void Un_decodage_d_un_autre_run_est_refuse()
    {
        var decoded = LangfuseFixtures.Decoded() with
        {
            Manifest = LangfuseFixtures.DecodeManifest() with { GeneratorRunId = "un-autre-run" },
        };

        Assert.Throws<InvalidOperationException>(
            () => LangfuseExportCommand.RequireExportable(LangfuseFixtures.Run(), decoded, PlainMetrics));
    }

    /// <summary>D5 : les pseudo-runs humains (scorés avec --subset) sont hors périmètre des phases 0-2.</summary>
    [Fact]
    public void Un_run_score_sur_un_sous_ensemble_est_refuse()
    {
        const string subset = """{"runId":"x","subsetFile":"elicitation.dev.jsonl","subsetOutcome":"solide"}""";

        var ex = Assert.Throws<InvalidOperationException>(
            () => LangfuseExportCommand.RequireExportable(LangfuseFixtures.Run(), LangfuseFixtures.Decoded(), subset));

        Assert.Contains("--subset", ex.Message);
    }

    /// <summary>Ruling 12 (2026-09-17) : mode events_only, renvoyer les spans d'une experiment déjà
    /// ingérée duplique les observations (constaté sur la trace 473ef7c265bd585074320c5a472c0884,
    /// 5 → 10 lignes après un second export identique).</summary>
    [Fact]
    public void Sans_experiment_existante_les_spans_sont_envoyes()
    {
        Assert.True(LangfuseExportCommand.ShouldSendSpans(existingExperimentId: null, resendSpans: false));
    }

    [Fact]
    public void Avec_experiment_existante_les_spans_ne_sont_pas_renvoyes()
    {
        Assert.False(LangfuseExportCommand.ShouldSendSpans(existingExperimentId: "cm123", resendSpans: false));
    }

    [Fact]
    public void Avec_experiment_existante_et_resend_spans_les_spans_sont_renvoyes()
    {
        Assert.True(LangfuseExportCommand.ShouldSendSpans(existingExperimentId: "cm123", resendSpans: true));
    }
}
