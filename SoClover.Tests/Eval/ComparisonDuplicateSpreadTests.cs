using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Les doublons inversés doivent être <b>indétectables</b> dans le flux de la séance.
/// <para>
/// Constat de la séance B du 2026-08-06 : le juge a repéré les répétitions et l'a signalé de
/// lui-même. La cause était structurelle — <c>basePlan.Concat(duplicates)</c> les posait en queue,
/// et <c>SpaceOut</c> ne les redistribuait pas : il garantit seulement qu'une clé ne réapparaît
/// pas avant dix positions, contrainte déjà satisfaite d'emblée puisque leurs originaux étaient
/// loin derrière. Les dix doublons occupaient donc les dix derniers items.
/// </para>
/// <para>
/// Un contrôle de cohérence que le juge repère cesse d'être un contrôle de cohérence : il mesure
/// la mémoire, ou pire, la volonté d'être cohérent. Les verdicts concernés deviennent
/// inexploitables — c'est ce qui est arrivé aux dix de cette séance.
/// </para>
/// </summary>
public class ComparisonDuplicateSpreadTests
{
    private const long Seed = 20260806001;

    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);
    private readonly RunContents _modelA;
    private readonly RunContents _modelB;
    private readonly RunContents _anchor;
    private readonly ElicitationContents _elicitation;

    public ComparisonDuplicateSpreadTests()
    {
        _modelA = HumanTestData.Run(_bench, "run-a", "modelA");
        _modelB = HumanTestData.Run(_bench, "run-b", "modelB");
        _anchor = HumanTestData.Run(_bench, "run-rnd", "aleatoire");
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        _elicitation = HumanTestData.Elicitation(_bench, annotated, assistCount: 12, passEvery: 8);
    }

    private IReadOnlyList<ComparisonPlanItem> Build(long seed = Seed, int targetCount = 100) =>
        ComparisonPlan.Build(
            _bench, _elicitation,
            _modelA, "run-a", _modelB, "run-b", _anchor, "run-rnd",
            seed, targetCount);

    private static List<int> DuplicateOrdinals(IReadOnlyList<ComparisonPlanItem> plan) =>
        plan.Select((item, index) => (item, index))
            .Where(p => p.item.DuplicateOf is not null)
            .Select(p => p.index)
            .ToList();

    /// <summary>Le défaut exact : les doublons ne forment plus le bloc terminal du lot.</summary>
    [Fact]
    public void Les_doublons_n_occupent_plus_la_fin_du_lot()
    {
        var plan = Build();
        var ordinals = DuplicateOrdinals(plan);

        var tail = Enumerable.Range(plan.Count - ComparisonPlan.DuplicateCount, ComparisonPlan.DuplicateCount);

        Assert.NotEqual(tail, ordinals);
        Assert.False(
            ordinals.All(o => o >= plan.Count - ComparisonPlan.DuplicateCount),
            "Les doublons occupent encore le bloc terminal : le juge les repérera.");
    }

    /// <summary>
    /// Trois doublons de suite se remarquent, même sans les compter. C'est le signal qui a mis la
    /// puce à l'oreille du juge.
    /// </summary>
    [Fact]
    public void Jamais_trois_doublons_consecutifs()
    {
        var ordinals = DuplicateOrdinals(Build());

        var longest = 1;
        var current = 1;
        for (var i = 1; i < ordinals.Count; i++)
        {
            current = ordinals[i] == ordinals[i - 1] + 1 ? current + 1 : 1;
            longest = Math.Max(longest, current);
        }

        Assert.True(longest < 3, $"{longest} doublons consécutifs dans le lot.");
    }

    /// <summary>
    /// Ils doivent être <b>répartis</b>, pas seulement décollés de la fin : leur empan couvre une
    /// large part de la seconde moitié, là où un doublon peut vivre sans violer la séparation
    /// minimale d'avec son original.
    /// </summary>
    [Fact]
    public void Les_doublons_sont_repartis_sur_la_seconde_moitie()
    {
        var plan = Build();
        var ordinals = DuplicateOrdinals(plan);

        var span = ordinals.Max() - ordinals.Min();
        var secondHalf = plan.Count / 2;

        Assert.True(span >= secondHalf / 2,
            $"empan des doublons {span} sur une seconde moitié de {secondHalf} : trop groupés.");
    }

    /// <summary>
    /// La contrainte qui borne la dispersion : un doublon trop proche de son original ne mesure
    /// plus la cohérence mais la mémoire immédiate. Elle reste tenue.
    /// </summary>
    [Fact]
    public void Chaque_doublon_reste_separe_de_son_original()
    {
        var plan = Build();
        var positionOf = plan
            .Select((item, index) => (item, index))
            .ToDictionary(p => p.item.ComparisonId, p => p.index, StringComparer.Ordinal);

        foreach (var (duplicate, index) in plan.Select((i, n) => (i, n)).Where(p => p.i.DuplicateOf is not null))
        {
            var original = positionOf[duplicate.DuplicateOf!];
            Assert.True(index - original >= ComparisonPlan.MinimumSeparation,
                $"doublon en {index} pour un original en {original} : écart {index - original}.");
        }
    }

    [Fact]
    public void La_repartition_reste_deterministe_a_seed_fixe()
    {
        Assert.Equal(DuplicateOrdinals(Build()), DuplicateOrdinals(Build()));
    }

    [Fact]
    public void Une_autre_seed_donne_une_autre_repartition()
    {
        Assert.NotEqual(DuplicateOrdinals(Build()), DuplicateOrdinals(Build(seed: Seed + 1)));
    }

    /// <summary>Le lot ne perd ni ne gagne d'item au passage.</summary>
    [Fact]
    public void Le_lot_conserve_ses_items_et_ses_doublons()
    {
        var plan = Build();

        Assert.Equal(ComparisonPlan.DuplicateCount, DuplicateOrdinals(plan).Count);
        Assert.Equal(plan.Count, plan.Select(i => i.ComparisonId).Distinct(StringComparer.Ordinal).Count());
    }
}
