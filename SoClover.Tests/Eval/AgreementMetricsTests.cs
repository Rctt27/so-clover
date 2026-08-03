using SoClover.Eval.Calibration;
using SoClover.Eval.Human;
using Xunit;

namespace SoClover.Tests.Eval;

public class AgreementMetricsTests
{
    // ---- Verdict du décodeur --------------------------------------------------

    [Theory]
    [InlineData(1.0, 0.5, JudgeSession.VerdictA)]
    [InlineData(0.5, 1.0, JudgeSession.VerdictB)]
    [InlineData(0.5, 0.5, JudgeSession.VerdictTie)]
    public void The_decoder_verdict_follows_the_sign_of_the_RBar_gap(double a, double b, string expected)
    {
        Assert.Equal(expected, AgreementMetrics.DecoderVerdict(a, b, epsilon: 0.0));
    }

    // ε n'est PAS un réglage libre : le modifier après avoir vu l'accord serait ajuster
    // l'instrument sur sa propre mesure. Il est donc explicite, et consigné au manifeste.
    [Fact]
    public void A_gap_below_epsilon_is_a_tie()
    {
        Assert.Equal(JudgeSession.VerdictTie, AgreementMetrics.DecoderVerdict(0.6, 0.5, epsilon: 0.2));
        Assert.Equal(JudgeSession.VerdictA, AgreementMetrics.DecoderVerdict(0.8, 0.5, epsilon: 0.2));
    }

    [Fact]
    public void An_undefined_RBar_makes_the_verdict_undecidable()
    {
        Assert.Null(AgreementMetrics.DecoderVerdict(null, 0.5, epsilon: 0.0));
        Assert.Null(AgreementMetrics.DecoderVerdict(0.5, null, epsilon: 0.0));
    }

    // ---- Accord ---------------------------------------------------------------

    [Fact]
    public void Agreement_is_one_on_a_fully_concordant_corpus()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictB, 0.0, 1.0),
            Couple("c-3", JudgeSession.VerdictA, 1.0, 0.5));

        Assert.Equal(3, report.DecidedCount);
        Assert.Equal(1.0, report.Agreement, precision: 10);
    }

    // LE risque du dénominateur : avec half_rate = 0,656, une large fraction des couples aura
    // R̄_A = R̄_B = 0,5. Le décodeur dit « tie », le couple sort du dénominateur, et la porte
    // finit par se jouer sur trente comparaisons.
    [Fact]
    public void Ties_on_either_side_leave_the_denominator()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictTie, 1.0, 0.0),
            Couple("c-3", JudgeSession.VerdictA, 0.5, 0.5));

        Assert.Equal(3, report.CoupleCount);
        Assert.Equal(1, report.DecidedCount);
        Assert.Equal(1.0, report.Agreement, precision: 10);
    }

    [Fact]
    public void Both_tie_rates_are_published_next_to_the_agreement()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictTie, 1.0, 0.0),
            Couple("c-3", JudgeSession.VerdictA, 0.5, 0.5),
            Couple("c-4", JudgeSession.VerdictB, 0.0, 1.0));

        Assert.Equal(0.25, report.HumanTieRate, precision: 10);
        Assert.Equal(0.25, report.DecoderTieRate, precision: 10);
    }

    // Un accord de 0,80 sur 28 couples a un IC qui traverse la porte, et le rapport doit le dire.
    [Fact]
    public void A_denominator_below_forty_couples_is_flagged_fragile()
    {
        var couples = Enumerable.Range(0, 39)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.True(Compute(couples).ThinDenominator);
    }

    [Fact]
    public void A_denominator_of_forty_couples_is_not_flagged()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.False(Compute(couples).ThinDenominator);
    }

    // D3 : R̄ indéfini d'un côté ne se compare pas. Le compter « tie » gonflerait le taux
    // d'égalité du décodeur ; le compter perdant serait faux.
    [Fact]
    public void A_couple_whose_clue_has_no_scored_decode_is_excluded_and_counted()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictA, null, 0.0));

        Assert.Equal(1, report.UnscorableCoupleCount);
        Assert.Equal(1, report.DecidedCount);
    }

    // ---- Cohen's κ ------------------------------------------------------------

    [Fact]
    public void Kappa_is_one_on_perfect_agreement_with_balanced_marginals()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i % 2 == 0
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictB, 0.0, 1.0))
            .ToArray();

        Assert.Equal(1.0, Compute(couples).Kappa, precision: 10);
    }

    [Fact]
    public void Kappa_is_near_zero_when_the_two_judges_are_independent()
    {
        // Humain alterne A/B ; décodeur alterne selon un cycle de 2 décalé d'un item sur deux :
        // accord observé 0,5, marginales équilibrées, donc p_e = 0,5 et κ = 0.
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple(
                $"c-{i}",
                i % 2 == 0 ? JudgeSession.VerdictA : JudgeSession.VerdictB,
                (i % 4) is 0 or 1 ? 1.0 : 0.0,
                (i % 4) is 0 or 1 ? 0.0 : 1.0))
            .ToArray();

        Assert.Equal(0.0, Compute(couples).Kappa, precision: 6);
    }

    // LE PARADOXE, documenté avant d'être rencontré : sur humanVsModel, l'humain gagnera
    // probablement la grande majorité des couples. p_e s'approche de p_o et κ s'effondre
    // MALGRÉ un accord élevé. Ce n'est pas un défaut du décodeur.
    [Fact]
    public void Kappa_collapses_on_skewed_marginals_despite_a_high_agreement()
    {
        var couples = new List<CalibrationCouple>();
        var rbar = new Dictionary<CalibrationClue, double?>();

        for (var i = 0; i < 100; i++)
        {
            // 90 % de victoires humaines des deux côtés ; les 10 % restants se répartissent
            // de sorte que l'accord observé vaille 0,90.
            // NOTE (déviation documentée du brief, cf. task-5-report.md) : les bornes 85/95 du
            // brief donnent un accord de 0,90 mais des marginales décodeur STRICTEMENT égales aux
            // marginales humain (90/10 des deux côtés) — p_e vaut alors 0,82 et κ = 0,444, qui ne
            // s'effondre PAS sous 0,40. Vérifié indépendamment (script Python, formule manuelle).
            // Les bornes 88/92 ci-dessous conservent le même accord de 0,90 et la même marginale
            // humaine de 0,90, mais skewent la marginale décodeur à 0,96 (p_e = 0,868), ce qui
            // reproduit le paradoxe décrit par le commentaire : κ = 0,242.
            var human = i < 90 ? JudgeSession.VerdictA : JudgeSession.VerdictB;
            var decoder = i < 88 || i >= 92 ? JudgeSession.VerdictA : JudgeSession.VerdictB;
            AddCouple(couples, rbar, $"c-{i}", human,
                decoder == JudgeSession.VerdictA ? 1.0 : 0.0,
                decoder == JudgeSession.VerdictA ? 0.0 : 1.0);
        }

        var report = AgreementMetrics.Compute(
            new CalibrationLot(couples.AsReadOnly(), [], []), rbar,
            epsilon: 0.0, bootstrapIterations: 200, seed: 1);

        Assert.Equal(0.90, report.Agreement, precision: 2);
        Assert.True(report.Kappa < 0.40,
            $"κ devrait s'effondrer sur marginales déséquilibrées, obtenu {report.Kappa}");
        // La table de contingence est la SEULE façon de distinguer « le décodeur est mauvais »
        // de « la statistique est mal conditionnée ».
        Assert.Equal(4, report.Contingency.Count);
        Assert.Equal(0.90, report.HumanMarginalA, precision: 2);
    }

    [Fact]
    public void Kappa_is_zero_rather_than_undefined_when_both_marginals_are_degenerate()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.Equal(0.0, Compute(couples).Kappa, precision: 10);
    }

    // L'INVARIANT CENTRAL du design P4-P5, testé explicitement : optionA/optionB sont les slots
    // CANONIQUES, presentedOrder dit seulement lequel fut affiché en position 1. Un κ calculé
    // sur les positions mesurerait le biais de position, pas l'accord.
    [Fact]
    public void Kappa_is_unchanged_when_the_presented_order_is_inverted()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i % 3 == 0
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictB, 0.0, 1.0))
            .ToArray();

        var straight = Compute(couples);
        // Le lot de calibration ne porte AUCUN champ d'ordre de présentation : l'inversion est
        // structurellement sans effet. Ce test est le garde-fou qui casse si quelqu'un
        // réintroduit presentedOrder dans CalibrationCouple.
        var inverted = Compute(couples.Reverse().ToArray());

        Assert.Equal(straight.Kappa, inverted.Kappa, precision: 10);
        Assert.Equal(straight.Agreement, inverted.Agreement, precision: 10);
    }

    // modelVsModel a des marginales naturellement plus équilibrées : c'est la famille la plus
    // informative pour κ, et c'est pour ça que le design P4-P5 lui a réservé ~40 couples.
    [Fact]
    public void Kappa_is_also_reported_per_family()
    {
        var couples = Enumerable.Range(0, 20)
            .Select(i => Couple($"h-{i}", JudgeSession.VerdictA, 1.0, 0.0,
                family: ComparisonFamilies.HumanVsModel))
            .Concat(Enumerable.Range(0, 20)
                .Select(i => Couple($"m-{i}", i % 2 == 0 ? JudgeSession.VerdictA : JudgeSession.VerdictB,
                    i % 2 == 0 ? 1.0 : 0.0, i % 2 == 0 ? 0.0 : 1.0,
                    family: ComparisonFamilies.ModelVsModel)))
            .ToArray();

        var report = Compute(couples);
        var modelVsModel = report.ByFamily.Single(f => f.Family == ComparisonFamilies.ModelVsModel);

        Assert.Equal(2, report.ByFamily.Count);
        Assert.Equal(20, modelVsModel.DecidedCount);
        Assert.Equal(1.0, modelVsModel.Kappa, precision: 10);
    }

    // Le PRD nomme κ ; la porte reste κ. PABAK est un DIAGNOSTIC — le publier comme porte
    // serait changer la règle en cours de partie.
    [Fact]
    public void Pabak_is_reported_as_a_diagnostic()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i < 36
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictA, 0.0, 1.0))
            .ToArray();

        var report = Compute(couples);

        Assert.Equal(2 * report.Agreement - 1, report.Pabak, precision: 10);
    }

    // ---- IC bootstrap ---------------------------------------------------------

    // Une porte à 0,75 franchie à 0,76 avec un IC [0,61 ; 0,88] doit être VISIBLEMENT fragile,
    // pas discrètement franchie.
    [Fact]
    public void The_agreement_confidence_interval_brackets_the_agreement()
    {
        var couples = Enumerable.Range(0, 60)
            .Select(i => Couple($"c-{i}", i % 5 == 0 ? JudgeSession.VerdictB : JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        var report = Compute(couples);

        Assert.True(report.AgreementCiLow <= report.Agreement);
        Assert.True(report.Agreement <= report.AgreementCiHigh);
        Assert.True(report.KappaCiLow <= report.Kappa);
        Assert.True(report.Kappa <= report.KappaCiHigh);
    }

    [Fact]
    public void The_confidence_intervals_are_deterministic_for_a_fixed_seed()
    {
        var couples = Enumerable.Range(0, 60)
            .Select(i => Couple($"c-{i}", i % 5 == 0 ? JudgeSession.VerdictB : JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        var a = Compute(couples);
        var b = Compute(couples);

        Assert.Equal(a.AgreementCiLow, b.AgreementCiLow);
        Assert.Equal(a.KappaCiHigh, b.KappaCiHigh);
    }

    // ---- Ancres ---------------------------------------------------------------

    // Attendu : le décodeur préfère l'indice réel à l'aléatoire sur ≥ 4 des 5 ancres.
    // Contrôle de bon sens, jamais une mesure.
    [Fact]
    public void Anchors_are_scored_apart_on_whether_the_decoder_prefers_the_real_clue()
    {
        var couples = new List<CalibrationCouple>();
        var anchors = new List<CalibrationCouple>();
        var rbar = new Dictionary<CalibrationClue, double?>();

        AddCouple(couples, rbar, "c-1", JudgeSession.VerdictA, 1.0, 0.0);
        for (var i = 0; i < 5; i++)
        {
            // Source A = modèle, source B = aléatoire. Le décodeur a raison quand il dit « A ».
            AddCouple(anchors, rbar, $"a-{i}", JudgeSession.VerdictA,
                i < 4 ? 1.0 : 0.0, i < 4 ? 0.0 : 1.0,
                family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random);
        }

        var report = AgreementMetrics.Compute(
            new CalibrationLot(couples.AsReadOnly(), anchors.AsReadOnly(), []), rbar,
            epsilon: 0.0, bootstrapIterations: 200, seed: 1);

        Assert.Equal(5, report.AnchorCount);
        Assert.Equal(4, report.AnchorCorrect);
        // Les ancres n'ont PAS gonflé l'accord principal.
        Assert.Equal(1, report.DecidedCount);
    }

    // ---- Fabriques ------------------------------------------------------------

    private static readonly Dictionary<string, (CalibrationCouple Couple, double? A, double? B)> Built = new();

    private static CalibrationCouple Couple(
        string id, string humanVerdict, double? rBarA, double? rBarB,
        string family = ComparisonFamilies.HumanVsModel)
    {
        var couple = new CalibrationCouple(
            ComparisonId: id, Family: family, BoardId: "dev-000", Direction: "Top",
            SourceA: ComparisonSources.Human, SourceB: ComparisonSources.Model,
            ClueA: $"{id}-a", ClueB: $"{id}-b", HumanVerdict: humanVerdict);

        Built[id] = (couple, rBarA, rBarB);
        return couple;
    }

    private static void AddCouple(
        List<CalibrationCouple> into, Dictionary<CalibrationClue, double?> rbar,
        string id, string humanVerdict, double? rBarA, double? rBarB,
        string family = ComparisonFamilies.HumanVsModel,
        string sourceA = ComparisonSources.Human,
        string sourceB = ComparisonSources.Model)
    {
        var couple = new CalibrationCouple(
            ComparisonId: id, Family: family, BoardId: "dev-000", Direction: "Top",
            SourceA: sourceA, SourceB: sourceB,
            ClueA: $"{id}-a", ClueB: $"{id}-b", HumanVerdict: humanVerdict);

        into.Add(couple);
        rbar[new CalibrationClue("dev-000", "Top", $"{id}-a")] = rBarA;
        rbar[new CalibrationClue("dev-000", "Top", $"{id}-b")] = rBarB;
    }

    private static AgreementReport Compute(params CalibrationCouple[] couples)
    {
        var rbar = new Dictionary<CalibrationClue, double?>();
        foreach (var couple in couples)
        {
            var (_, a, b) = Built[couple.ComparisonId];
            rbar[new CalibrationClue(couple.BoardId, couple.Direction, couple.ClueA)] = a;
            rbar[new CalibrationClue(couple.BoardId, couple.Direction, couple.ClueB)] = b;
        }

        return AgreementMetrics.Compute(
            new CalibrationLot(couples.ToList().AsReadOnly(), [], []), rbar,
            epsilon: AgreementMetrics.DefaultEpsilon,
            bootstrapIterations: 200,
            seed: AgreementMetrics.DefaultBootstrapSeed);
    }
}
