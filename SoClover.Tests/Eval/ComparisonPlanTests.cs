using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ComparisonPlanTests
{
    private const long Seed = 20260730001;

    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);
    private readonly RunContents _modelA;
    private readonly RunContents _modelB;
    private readonly RunContents _anchor;
    private readonly ElicitationContents _elicitation;
    private readonly IReadOnlyList<PlanItem> _annotated;

    public ComparisonPlanTests()
    {
        _modelA = HumanTestData.Run(_bench, "run-a", "modelA");
        _modelB = HumanTestData.Run(_bench, "run-b", "modelB");
        _anchor = HumanTestData.Run(_bench, "run-rnd", "aleatoire");
        _annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        _elicitation = HumanTestData.Elicitation(_bench, _annotated, assistCount: 12, passEvery: 8);
    }

    private IReadOnlyList<ComparisonPlanItem> Build(int targetCount = 100) =>
        ComparisonPlan.Build(
            _bench, _elicitation,
            _modelA, "run-a", _modelB, "run-b", _anchor, "run-rnd",
            Seed, targetCount);

    [Fact]
    public void Le_plan_est_reproductible_a_seed_fixe()
    {
        // Comparaison champ par champ, ReferenceWords inclus : ComparisonPlanItem est un record
        // dont ReferenceWords (IReadOnlyList<string>) est comparé PAR RÉFÉRENCE par l'Equals
        // généré par le compilateur. `Assert.Equal(Build(), Build())` sur les deux listes serait
        // structurellement inerte sur ce champ (les deux appels partagent le même _bench, donc la
        // même instance de liste) — un `.ToList()` défensif ajouté demain dans ComparisonPlan.Build
        // romprait le partage de référence et ferait rougir ce test malgré un contenu identique.
        // Patron : BenchFileTests.cs:32-41, HumanFileTests.cs.
        var first = Build();
        var second = Build();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].ComparisonId, second[i].ComparisonId);
            Assert.Equal(first[i].Family, second[i].Family);
            Assert.Equal(first[i].BoardId, second[i].BoardId);
            Assert.Equal(first[i].Direction, second[i].Direction);
            Assert.Equal(first[i].ReferenceWords, second[i].ReferenceWords);
            Assert.Equal(first[i].OptionA, second[i].OptionA);
            Assert.Equal(first[i].OptionB, second[i].OptionB);
            Assert.Equal(first[i].PresentedOrder, second[i].PresentedOrder);
            Assert.Equal(first[i].DuplicateOf, second[i].DuplicateOf);
        }
    }

    [Fact]
    public void Les_quatre_familles_sont_presentes_avec_leurs_volumes()
    {
        var plan = Build();
        var byFamily = plan.Where(i => i.DuplicateOf is null)
            .GroupBy(i => i.Family)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(ComparisonPlan.AnchorCount, byFamily[ComparisonFamilies.Anchor]);
        Assert.InRange(byFamily[ComparisonFamilies.HumanVsModel], 30, 40);
        Assert.InRange(byFamily[ComparisonFamilies.HumanVsAssisted], 8, 15);
        Assert.True(byFamily[ComparisonFamilies.ModelVsModel] > 0);
        Assert.Equal(90, plan.Count(i => i.DuplicateOf is null));
        Assert.Equal(ComparisonPlan.DuplicateCount, plan.Count(i => i.DuplicateOf is not null));
    }

    [Fact]
    public void modelVsModel_nest_jamais_tire_sur_une_direction_annotee()
    {
        var annotated = _elicitation.Elicitations.Select(e => (e.BoardId, e.Direction)).ToHashSet();

        foreach (var item in Build().Where(i => i.Family == ComparisonFamilies.ModelVsModel))
            Assert.DoesNotContain((item.BoardId, item.Direction), annotated);
    }

    [Fact]
    public void Les_ancres_opposent_le_modele_au_plancher_aleatoire()
    {
        foreach (var item in Build().Where(i => i.Family == ComparisonFamilies.Anchor))
        {
            var sources = new[] { item.OptionA.Source, item.OptionB.Source };
            Assert.Contains(ComparisonSources.Model, sources);
            Assert.Contains(ComparisonSources.Random, sources);
        }
    }

    [Fact]
    public void Lequilibrage_des_ordres_de_presentation_est_exact()
    {
        var plan = Build();
        var ab = plan.Count(i => i.PresentedOrder == PresentedOrders.Ab);
        var ba = plan.Count(i => i.PresentedOrder == PresentedOrders.Ba);

        Assert.Equal(plan.Count % 2 == 0 ? 0 : 1, Math.Abs(ab - ba));
    }

    [Fact]
    public void Les_doublons_portent_lordre_oppose_et_un_identifiant_distinct()
    {
        var plan = Build();
        var byId = plan.ToDictionary(i => i.ComparisonId, StringComparer.Ordinal);

        var duplicates = plan.Where(i => i.DuplicateOf is not null).ToList();
        Assert.Equal(ComparisonPlan.DuplicateCount, duplicates.Count);

        foreach (var duplicate in duplicates)
        {
            var original = byId[duplicate.DuplicateOf!];
            Assert.Equal(PresentedOrders.Invert(original.PresentedOrder), duplicate.PresentedOrder);
            Assert.Equal(original.OptionA, duplicate.OptionA);
            Assert.Equal(original.OptionB, duplicate.OptionB);
            Assert.NotEqual(original.ComparisonId, duplicate.ComparisonId);
        }
    }

    [Fact]
    public void Deux_comparaisons_de_meme_paire_cible_sont_separees_dau_moins_dix_items()
    {
        var plan = Build();
        var lastSeen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < plan.Count; i++)
        {
            var key = plan[i].BoardId + "|" + plan[i].Direction;
            if (lastSeen.TryGetValue(key, out var previous))
                Assert.True(i - previous >= ComparisonPlan.MinimumSeparation,
                    $"écart de {i - previous} entre deux comparaisons de {key}");
            lastSeen[key] = i;
        }
    }

    [Fact]
    public void Un_couple_dont_les_deux_indices_sont_identiques_est_ecarte()
    {
        // Un run B strictement identique au run A : toutes les comparaisons modelVsModel
        // deviennent vides de sens et doivent disparaître du plan.
        var identical = HumanTestData.Run(_bench, "run-a-bis", "modelA");

        var plan = ComparisonPlan.Build(
            _bench, _elicitation, _modelA, "run-a", identical, "run-a-bis",
            _anchor, "run-rnd", Seed, targetCount: 100);

        Assert.Empty(plan.Where(i => i.Family == ComparisonFamilies.ModelVsModel));
        Assert.All(plan, i => Assert.NotEqual(i.OptionA.Clue, i.OptionB.Clue));
    }

    [Fact]
    public void Le_comparisonId_est_stable_entre_deux_reconstructions_et_ignore_lordre_des_slots()
    {
        var human = new ComparisonOption(ComparisonSources.Human, null, "Pédiatre");
        var model = new ComparisonOption(ComparisonSources.Model, "run-a", "Hôpital");

        Assert.Equal(
            ComparisonPlan.ComputeComparisonId("dev-007", "Top", human, model),
            ComparisonPlan.ComputeComparisonId("dev-007", "Top", model, human));

        Assert.NotEqual(
            ComparisonPlan.ComputeComparisonId("dev-007", "Top", human, model),
            ComparisonPlan.ComputeComparisonId("dev-007", "Left", human, model));
    }

    [Fact]
    public void Les_options_canoniques_sont_ordonnees_par_provenance()
    {
        foreach (var item in Build())
            Assert.True(
                ComparisonSources.Rank(item.OptionA.Source) <= ComparisonSources.Rank(item.OptionB.Source),
                $"{item.OptionA.Source} devrait précéder {item.OptionB.Source}");
    }

    [Fact]
    public void SpaceOut_repousse_les_repetitions_sans_perdre_ni_dupliquer_ditem()
    {
        string[] items = ["a", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k"];

        var spaced = ComparisonPlan.SpaceOut(items, minGap: 3, keyOf: s => s);

        Assert.Equal(items.Length, spaced.Count);
        Assert.Equal(items.OrderBy(s => s, StringComparer.Ordinal), spaced.OrderBy(s => s, StringComparer.Ordinal));
        Assert.True(spaced.IndexOf("a") + 3 <= spaced.LastIndexOf("a"));
    }
}
