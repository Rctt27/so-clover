using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// La provenance du prompt est une information d'audit : elle ne doit changer ni le hash8 d'un run
/// (qui signale un re-run de même configuration), ni la forme des manifestes existants.
/// </summary>
public class PromptProvenanceManifestTests
{
    [Fact]
    public void Sans_provenance_le_manifeste_de_run_ne_porte_aucune_cle_prompt()
    {
        var json = EvalJson.Serialize(LangfuseFixtures.RunManifest());

        Assert.DoesNotContain("\"prompt\":", json);
    }

    [Fact]
    public void La_provenance_ne_change_pas_le_hash8()
    {
        var bare = LangfuseFixtures.RunManifest();
        var withProvenance = LangfuseFixtures.RunManifest(LangfuseFixtures.LangfuseClue);

        Assert.Equal(RunFile.ComputeHash8(bare), RunFile.ComputeHash8(withProvenance));
    }

    [Fact]
    public void Un_manifeste_de_run_avec_provenance_se_relit_a_l_identique()
    {
        var manifest = LangfuseFixtures.RunManifest(LangfuseFixtures.LangfuseClue);

        Assert.Equal(manifest, EvalJson.Deserialize<RunManifest>(EvalJson.Serialize(manifest)));
    }

    [Fact]
    public void Un_manifeste_de_run_anterieur_se_relit_avec_une_provenance_nulle()
    {
        var legacy = EvalJson.Serialize(LangfuseFixtures.RunManifest());

        Assert.Null(EvalJson.Deserialize<RunManifest>(legacy).Prompt);
    }

    [Fact]
    public void Le_manifeste_de_decodage_porte_la_provenance_du_prompt_clue_et_se_relit()
    {
        var manifest = LangfuseFixtures.DecodeManifest(LangfuseFixtures.LangfuseClue);
        var json = EvalJson.Serialize(manifest);

        Assert.Contains("\"cluePrompt\":", json);
        Assert.DoesNotContain("\"boardPrompt\":", json);
        Assert.Equal(manifest, EvalJson.Deserialize<DecodeManifest>(json));
    }
}
