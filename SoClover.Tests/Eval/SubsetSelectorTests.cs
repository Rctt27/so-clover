using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class SubsetSelectorTests
{
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);

    [Fact]
    public void Sans_drapeau_toutes_les_issues_sont_retenues()
    {
        var outcomes = SubsetSelector.ParseOutcomes(null);

        Assert.Equal(3, outcomes.Count);
        Assert.Contains(Outcomes.Pass, outcomes);
    }

    [Fact]
    public void Le_drapeau_filtre_sur_les_issues_demandees()
    {
        var elicitation = HumanTestData.Elicitation(
            _bench, ElicitationPlan.Build(_bench, seed: 42, targetCount: 40), passEvery: 8);

        var all = SubsetSelector.FromElicitation(elicitation, SubsetSelector.ParseOutcomes(null));
        var resolved = SubsetSelector.FromElicitation(
            elicitation, SubsetSelector.ParseOutcomes("solide,tiede"));

        Assert.Equal(40, all.Count);
        Assert.True(resolved.Count < all.Count);
        Assert.All(resolved, key => Assert.Contains(key, all));
    }

    [Fact]
    public void Une_issue_inconnue_est_refusee()
    {
        Assert.Throws<ArgumentException>(() => SubsetSelector.ParseOutcomes("solide,brulant"));
    }
}
