using SoClover.Eval.Langfuse;
using Xunit;

namespace SoClover.Tests.Eval;

public class PromptSyncPlannerTests
{
    private const string V4 = "---\nversion: 4\n---\n# SYSTEM\nDevine.\n";
    private const string V4Edited = "---\nversion: 4\n---\n# SYSTEM\nDevine autrement.\n";
    private const string V5 = "---\nversion: 5\n---\n# SYSTEM\nDevine mieux.\n";

    private static LangfusePrompt P(int version, string content) => new("decoder-fr-clue", version, content, []);

    [Fact]
    public void Un_prompt_jamais_publie_est_a_creer()
    {
        Assert.Equal(new PromptSyncDecision(PromptSyncOutcome.Create, 4, null), PromptSyncPlanner.Decide(V4, []));
    }

    [Fact]
    public void Un_contenu_deja_publie_n_est_pas_republie_meme_en_CRLF()
    {
        var decision = PromptSyncPlanner.Decide(V4.Replace("\n", "\r\n"), [P(1, V4), P(2, V5)]);

        Assert.Equal(new PromptSyncDecision(PromptSyncOutcome.AlreadyPresent, 4, 1), decision);
    }

    [Fact]
    public void Une_nouvelle_version_declaree_est_a_creer()
    {
        Assert.Equal(PromptSyncOutcome.Create, PromptSyncPlanner.Decide(V5, [P(1, V4)]).Outcome);
    }

    [Fact]
    public void Un_contenu_different_sous_une_version_deja_publiee_est_un_conflit()
    {
        Assert.Equal(new PromptSyncDecision(PromptSyncOutcome.Conflict, 4, 1), PromptSyncPlanner.Decide(V4Edited, [P(1, V4)]));
    }

    [Fact]
    public void Un_fichier_local_sans_version_declaree_est_refuse()
    {
        Assert.Throws<InvalidOperationException>(() => PromptSyncPlanner.Decide("# SYSTEM\nrien", []));
    }
}
