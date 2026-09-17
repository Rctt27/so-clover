using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// La provenance du prompt est une information d'audit : à <c>PromptFile</c> égal, elle ne change pas
/// le hash8 d'un run (qui signale un re-run de même configuration), ni la forme des manifestes
/// existants. Le chemin <c>PromptFile</c>, lui, dépend de la source et entre dans le hash8.
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
    public void A_PromptFile_egal_la_provenance_ne_change_pas_le_hash8()
    {
        var bare = LangfuseFixtures.RunManifest();
        var withProvenance = LangfuseFixtures.RunManifest(LangfuseFixtures.LangfuseClue);

        Assert.Equal(RunFile.ComputeHash8(bare), RunFile.ComputeHash8(withProvenance));
    }

    // Ruling 13 : comportement documenté, pas souhaité. Le hash8 hache PromptFile, dont le chemin
    // dépend de la source du prompt (et de Debug/Release pour la source fichier).
    [Fact]
    public void Le_hash8_depend_du_chemin_PromptFile_donc_de_la_source_du_prompt()
    {
        var fromFile = LangfuseFixtures.RunManifest() with
        {
            PromptFile = ".../bin/Debug/net9.0/Infrastructure/AI/Prompts/fr/board-clues-per-direction.md",
        };
        var fromLangfuse = LangfuseFixtures.RunManifest() with
        {
            PromptFile = "eval/prompts/resolved/aaaaaaaaaaaa/fr/board-clues-per-direction.md",
        };

        Assert.NotEqual(RunFile.ComputeHash8(fromFile), RunFile.ComputeHash8(fromLangfuse));
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
