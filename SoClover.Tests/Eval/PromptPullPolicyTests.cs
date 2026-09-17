using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using Xunit;

namespace SoClover.Tests.Eval;

public class PromptPullPolicyTests
{
    private const string V4 = "---\nversion: 4\n---\n# SYSTEM\nDevine.\n";
    private const string V4Edited = "---\nversion: 4\n---\n# SYSTEM\nDevine autrement.\n";
    private const string V3 = "---\nversion: 3\n---\n# SYSTEM\nAncien.\n";
    private const string V5 = "---\nversion: 5\n---\n# SYSTEM\nDevine mieux.\n";

    private static LangfusePrompt P(int version, string content) => new("decoder-fr-clue", version, content, []);

    [Fact]
    public void Une_version_declaree_superieure_est_acceptee()
    {
        Assert.Equal(5, PromptPullPolicy.Check(V4, P(7, V5)));
    }

    [Fact]
    public void Un_contenu_identique_n_a_rien_a_tirer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PromptPullPolicy.Check(V4.Replace("\n", "\r\n"), P(1, V4)));
        Assert.Contains("rien à tirer", ex.Message);
    }

    [Theory]
    [InlineData(V4Edited)]
    [InlineData(V3)]
    public void Un_contenu_different_sans_version_superieure_est_refuse(string incoming)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PromptPullPolicy.Check(V4, P(2, incoming)));
        Assert.Contains("garde 0", ex.Message);
    }

    [Fact]
    public void Doctor_signale_l_egalite_l_ecart_et_l_absence()
    {
        var prompt = SoCloverPrompt.DecoderFrClue;

        Assert.Contains("= fichier embarqué", PromptDrift.Describe(prompt, V4, P(1, V4)));
        Assert.Contains("ÉCART", PromptDrift.Describe(prompt, V4, P(2, V5)));
        Assert.Contains("langfuse-sync --prompts", PromptDrift.Describe(prompt, V4, null));
    }
}
