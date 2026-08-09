using SoClover.Eval.Human;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ElicitationPlanTests
{
    private const long Seed = 20260729001;

    [Fact]
    public void Le_plan_est_reproductible_a_seed_fixe()
    {
        var bench = HumanTestData.Bench(boardCount: 40);

        var first = ElicitationPlan.Build(bench, Seed, targetCount: 40);
        var second = ElicitationPlan.Build(bench, Seed, targetCount: 40);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Les_directions_tirees_sont_toutes_distinctes()
    {
        var bench = HumanTestData.Bench(boardCount: 40);

        var plan = ElicitationPlan.Build(bench, Seed, targetCount: 40);

        Assert.Equal(40, plan.Count);
        Assert.Equal(40, plan.Distinct().Count());
    }

    [Fact]
    public void Un_plan_de_taille_N_est_un_prefixe_stable_dun_plan_plus_grand()
    {
        var bench = HumanTestData.Bench(boardCount: 40);

        var small = ElicitationPlan.Build(bench, Seed, targetCount: 40);
        var large = ElicitationPlan.Build(bench, Seed, targetCount: 60);

        Assert.Equal(small, large.Take(40));
    }

    [Fact]
    public void Le_plan_depend_du_benchHash()
    {
        var one = HumanTestData.Bench(boardCount: 40, benchHash: "aaaaaaaaaaaa");
        var other = HumanTestData.Bench(boardCount: 40, benchHash: "cccccccccccc");

        Assert.NotEqual(
            ElicitationPlan.Build(one, Seed, 40),
            ElicitationPlan.Build(other, Seed, 40));
    }

    [Fact]
    public void Le_plan_depend_du_seed()
    {
        var bench = HumanTestData.Bench(boardCount: 40);

        Assert.NotEqual(
            ElicitationPlan.Build(bench, Seed, 40),
            ElicitationPlan.Build(bench, Seed + 1, 40));
    }

    [Fact]
    public void Un_targetCount_superieur_au_banc_est_refuse()
    {
        var bench = HumanTestData.Bench(boardCount: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => ElicitationPlan.Build(bench, Seed, targetCount: 9));
    }
}
