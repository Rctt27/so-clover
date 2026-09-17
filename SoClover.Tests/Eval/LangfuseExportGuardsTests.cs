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
}
