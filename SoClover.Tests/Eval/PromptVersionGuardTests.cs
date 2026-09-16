using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Garde 0 de la skill soclover-eval, appliquée par une machine : un contenu différent sous un
/// même <c>version:</c> déclaré est l'incident fondateur du cycle P6 — deux décodeurs sous la
/// même empreinte.
/// </summary>
public class PromptVersionGuardTests
{
    private const string V4 = "---\nversion: 4\n---\n# SYSTEM\nDevine la paire.\n";
    private const string V4Edited = "---\nversion: 4\n---\n# SYSTEM\nDevine LA paire.\n";
    private const string V5 = "---\nversion: 5\n---\n# SYSTEM\nDevine la paire, autrement.\n";

    private static LangfusePrompt P(int langfuseVersion, string content) =>
        new("decoder-fr-clue", langfuseVersion, content, []);

    [Fact]
    public void Normaliser_rend_le_meme_sha_pour_CRLF_et_LF()
    {
        Assert.Equal(PromptContent.Sha256(V4), PromptContent.Sha256(V4.Replace("\n", "\r\n")));
        Assert.Equal(64, PromptContent.Sha256(V4).Length);
    }

    [Fact]
    public void La_version_declaree_se_lit_dans_le_frontmatter()
    {
        Assert.Equal(4, PromptContent.DeclaredVersion(V4));
        Assert.Equal(4, PromptContent.DeclaredVersion(V4.Replace("\n", "\r\n")));
        Assert.Null(PromptContent.DeclaredVersion("# SYSTEM\nsans frontmatter"));
    }

    [Fact]
    public void Un_historique_coherent_rend_la_version_declaree()
    {
        var resolved = P(2, V5);

        Assert.Equal(5, PromptVersionGuard.RequireConsistent(resolved, [P(1, V4), resolved]));
    }

    [Fact]
    public void Un_meme_contenu_republie_sous_une_autre_version_Langfuse_n_est_pas_un_conflit()
    {
        var resolved = P(3, V4);

        Assert.Equal(4, PromptVersionGuard.RequireConsistent(resolved, [P(1, V4), P(2, V5), resolved]));
    }

    [Fact]
    public void Un_contenu_modifie_sans_bump_est_refuse_en_nommant_les_versions_en_conflit()
    {
        var resolved = P(2, V4Edited);

        var ex = Assert.Throws<InvalidOperationException>(
            () => PromptVersionGuard.RequireConsistent(resolved, [P(1, V4), resolved]));

        Assert.Contains("version: 4", ex.Message);
        Assert.Contains("#1", ex.Message);
    }

    [Fact]
    public void Le_conflit_est_detecte_aussi_quand_on_resout_la_version_la_plus_ancienne()
    {
        var resolved = P(1, V4);

        Assert.Throws<InvalidOperationException>(
            () => PromptVersionGuard.RequireConsistent(resolved, [resolved, P(2, V4Edited)]));
    }

    [Fact]
    public void Un_prompt_sans_version_declaree_est_refuse()
    {
        var resolved = P(1, "# SYSTEM\nsans frontmatter");

        Assert.Throws<InvalidOperationException>(
            () => PromptVersionGuard.RequireConsistent(resolved, [resolved]));
    }
}
