using SoClover.Eval.Human;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Séance D « devineur » : le plan est reconstruit à chaque démarrage depuis
/// <c>(benchHash, seed, boards éligibles, run source)</c>, jamais persisté — même invariant que
/// <see cref="ElicitationPlan"/>.
/// </summary>
public class GuessingPlanTests
{
    private const long Seed = 20260807001;

    private static readonly IReadOnlySet<string> Rien = new HashSet<string>();

    [Fact]
    public void Le_plan_est_reproductible_a_seed_fixe()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "run-a", "gemma");

        Assert.Equal(
            GuessingPlan.Build(bench, run, Rien, Seed),
            GuessingPlan.Build(bench, run, Rien, Seed));
    }

    [Fact]
    public void Le_plan_depend_du_seed()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "run-a", "gemma");

        Assert.NotEqual(
            GuessingPlan.Build(bench, run, Rien, Seed),
            GuessingPlan.Build(bench, run, Rien, Seed + 1));
    }

    [Fact]
    public void Les_boards_exclus_ne_paraissent_jamais()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "run-a", "gemma");
        var exclus = new HashSet<string> { "dev-000", "dev-003", "dev-007" };

        var plan = GuessingPlan.Build(bench, run, exclus, Seed);

        Assert.All(plan, item => Assert.DoesNotContain(item.BoardId, exclus));
        Assert.Equal(8 * 4, plan.Count);
    }

    [Fact]
    public void Une_direction_sans_indice_valide_est_ignoree()
    {
        var bench = HumanTestData.Bench(boardCount: 4);
        var run = HumanTestData.Run(bench, "run-a", "gemma");
        var ampute = run with
        {
            Attempts = run.Attempts
                .Select(a => a.BoardId == "dev-001" && a.Direction == "Top"
                    ? a with { Valid = false, Clue = null, FailureKind = "unparseable" }
                    : a)
                .ToList(),
        };

        var plan = GuessingPlan.Build(bench, ampute, Rien, Seed);

        Assert.Equal(4 * 4 - 1, plan.Count);
        Assert.DoesNotContain(plan, i => i.BoardId == "dev-001" && i.Direction == "Top");
    }

    [Fact]
    public void Chaque_item_porte_lindice_du_run()
    {
        var bench = HumanTestData.Bench(boardCount: 3);
        var run = HumanTestData.Run(bench, "run-a", "gemma");

        var plan = GuessingPlan.Build(bench, run, Rien, Seed);

        Assert.All(plan, item =>
            Assert.Equal($"gemma-{item.BoardId}-{item.Direction}", item.Clue));
    }

    /// <summary>
    /// La mitigation déclarée au pré-enregistrement (amendement du 2026-08-07) : l'humain garde la
    /// mémoire d'un board d'une direction à l'autre, le décodeur non. Les items sont dispersés pour
    /// que cette mémoire soit la plus froide possible — deux directions d'un même board ne peuvent
    /// jamais se suivre.
    /// </summary>
    [Fact]
    public void Deux_directions_dun_meme_board_ne_se_suivent_jamais()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "run-a", "gemma");

        var plan = GuessingPlan.Build(bench, run, Rien, Seed);

        Assert.Equal(11 * 4, plan.Count);
        for (var i = 1; i < plan.Count; i++)
            Assert.NotEqual(plan[i - 1].BoardId, plan[i].BoardId);
    }

    [Fact]
    public void Toutes_les_directions_eligibles_sont_presentes_une_seule_fois()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "run-a", "gemma");

        var plan = GuessingPlan.Build(bench, run, Rien, Seed);

        Assert.Equal(plan.Count, plan.Select(i => (i.BoardId, i.Direction)).Distinct().Count());
    }
}
