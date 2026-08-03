using SoClover.Eval.Calibration;
using SoClover.Eval.Human;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationSetTests
{
    [Fact]
    public void Keeps_one_couple_per_comparison_with_its_human_verdict()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA),
            HumanTestData.Comparison("c-002", JudgeSession.VerdictB, boardId: "dev-001", itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Equal(2, lot.Couples.Count);
        Assert.Equal(JudgeSession.VerdictA, lot.Couples[0].HumanVerdict);
        Assert.Equal(JudgeSession.VerdictB, lot.Couples[1].HumanVerdict);
    }

    // Le doublon inversé désigne le MÊME couple. Le compter deux fois doublerait son poids
    // dans l'accord et dans κ.
    [Fact]
    public void An_inverted_duplicate_counts_once_and_the_last_verdict_wins()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, itemOrdinal: 1),
            HumanTestData.Comparison(
                "c-001-r", JudgeSession.VerdictB, presentedOrder: PresentedOrders.Ba,
                duplicateOf: "c-001", itemOrdinal: 20));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Equal(JudgeSession.VerdictB, lot.Couples[0].HumanVerdict);
        Assert.Equal("c-001", lot.Couples[0].ComparisonId);
    }

    // Un re-jugement AJOUTE une ligne sous le même comparisonId : c'est le lecteur qui retient
    // la dernière — règle déjà en place dans HumanFile.
    [Fact]
    public void A_rejudged_comparison_keeps_only_its_last_verdict()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, itemOrdinal: 1),
            HumanTestData.Comparison("c-001", JudgeSession.VerdictTie, itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Equal(JudgeSession.VerdictTie, lot.Couples[0].HumanVerdict);
    }

    // Les ancres restent un contrôle de bon sens du décodeur, pas une mesure : leur écart de
    // qualité est évident et gonflerait artificiellement l'accord et κ.
    [Fact]
    public void Anchors_are_kept_apart_from_the_main_families()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA),
            HumanTestData.Comparison(
                "c-a01", JudgeSession.VerdictA, family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random,
                clueA: "indice-modele", clueB: "indice-aleatoire", boardId: "dev-002", itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Single(lot.Anchors);
        Assert.Equal(ComparisonFamilies.Anchor, lot.Anchors[0].Family);
        Assert.DoesNotContain(lot.Couples, c => c.Family == ComparisonFamilies.Anchor);
    }

    [Fact]
    public void The_three_main_families_all_enter_the_main_lot()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, family: ComparisonFamilies.HumanVsModel),
            HumanTestData.Comparison(
                "c-002", JudgeSession.VerdictA, family: ComparisonFamilies.ModelVsModel,
                sourceA: ComparisonSources.Model, boardId: "dev-001", itemOrdinal: 2),
            HumanTestData.Comparison(
                "c-003", JudgeSession.VerdictA, family: ComparisonFamilies.HumanVsAssisted,
                sourceB: ComparisonSources.Assisted, boardId: "dev-002", itemOrdinal: 3));

        var lot = CalibrationSet.Build(contents);

        Assert.Equal(3, lot.Couples.Count);
        Assert.Equal(CalibrationSet.MainFamilies.Order().ToList(),
            lot.Couples.Select(c => c.Family).Distinct().Order().ToList());
    }

    // Le lot d'indices porte les DEUX options de chaque couple, ancres comprises : le décodeur
    // doit évaluer tout ce qui sera comparé.
    [Fact]
    public void The_clue_lot_covers_both_options_of_every_couple_including_anchors()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, clueA: "Alpha", clueB: "Beta"),
            HumanTestData.Comparison(
                "c-a01", JudgeSession.VerdictA, family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random,
                clueA: "Gamma", clueB: "Delta", boardId: "dev-002", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues.Select(c => c.Clue).ToList();

        Assert.Equal(4, clues.Count);
        Assert.Contains("Alpha", clues);
        Assert.Contains("Delta", clues);
    }

    // ~180 indices distincts × 5 décodages ≈ 900 appels : un indice décodé deux fois, c'est
    // du temps de LM Studio jeté, et deux R̄ différents pour le même indice.
    [Fact]
    public void A_clue_shared_by_two_couples_is_decoded_once()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, clueA: "Humain", clueB: "ModeleA"),
            HumanTestData.Comparison(
                "c-002", JudgeSession.VerdictB, family: ComparisonFamilies.ModelVsModel,
                sourceA: ComparisonSources.Model, clueA: "ModeleA", clueB: "ModeleB", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues;

        Assert.Equal(3, clues.Count);
        Assert.Single(clues.Where(c => c.Clue == "ModeleA"));
    }

    // Le même mot sur deux directions différentes reste deux indices : la paire de référence
    // n'est pas la même, donc R non plus.
    [Fact]
    public void The_same_word_on_two_directions_is_two_distinct_clues()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, direction: "Top", clueA: "Mer"),
            HumanTestData.Comparison("c-002", JudgeSession.VerdictA, direction: "Left", clueA: "Mer", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues.Where(c => c.Clue == "Mer").ToList();

        Assert.Equal(2, clues.Count);
    }

    [Fact]
    public void A_couple_whose_two_clues_are_identical_is_dropped()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictTie, clueA: "Même", clueB: "Même"));

        var lot = CalibrationSet.Build(contents);

        Assert.Empty(lot.Couples);
        Assert.Empty(lot.Clues);
    }

    [Fact]
    public void The_lot_is_ordered_deterministically()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-002", JudgeSession.VerdictA, boardId: "dev-005", direction: "Left", itemOrdinal: 1),
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, boardId: "dev-001", direction: "Top", itemOrdinal: 2));

        var first = CalibrationSet.Build(contents).Clues.Select(c => (c.BoardId, c.Direction, c.Clue)).ToList();
        var second = CalibrationSet.Build(contents).Clues.Select(c => (c.BoardId, c.Direction, c.Clue)).ToList();

        Assert.Equal(first, second);
        Assert.Equal("dev-001", first[0].BoardId);
    }

    // ---- R̄ par indice --------------------------------------------------------

    [Fact]
    public void RBar_averages_the_scored_decodes_of_a_clue()
    {
        var decodes = Decodes(("Pédiatre", 1.0), ("Pédiatre", 0.5), ("Pédiatre", 0.0));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Equal(0.5, rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]!.Value, precision: 10);
    }

    // Invariant déjà tenu par RunMetrics, et pour la même raison : un décodeur qui ne sait pas
    // répondre au format n'est pas un décodeur qui se trompe.
    [Fact]
    public void A_format_failure_leaves_the_denominator_rather_than_counting_zero()
    {
        var decodes = Decodes(("Pédiatre", 1.0), ("Pédiatre", null), ("Pédiatre", 1.0));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Equal(1.0, rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]!.Value, precision: 10);
    }

    [Fact]
    public void A_clue_without_a_single_scored_decode_has_no_RBar()
    {
        var decodes = Decodes(("Pédiatre", null), ("Pédiatre", null));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Null(rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]);
    }

    private static CalibrationContents Decodes(params (string Clue, double? R)[] entries)
    {
        var lines = entries.Select((e, i) => new CalibrationDecode(
            Kind: CalibrationDecode.LineKind,
            BoardId: "dev-007",
            Direction: "Top",
            Clue: e.Clue,
            DecodeIndex: i,
            Picked: e.R is null ? null : ["Chirurgien", "Enfant"],
            R: e.R,
            ShuffleSeed: "1",
            DecodeFailureKind: e.R is null ? "unparseable" : null,
            LatencyMs: 100)).ToList();

        return new CalibrationContents(
            CalibrationFileTestsManifest(), lines.AsReadOnly());
    }

    private static CalibrationManifest CalibrationFileTestsManifest() => new(
        Kind: "manifest", CalibrationId: "20260805-3f2a91c4e0d1",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.dev.jsonl", BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "aaaaaaaaaaaa", CoupleCount: 0, ClueCount: 0,
        DecoderFingerprint: "3f2a91c4e0d1", Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1", ModelId: "m", ModelSnapshotDate: null,
        ProviderModelListHash: null, Temperature: 0.3, TopP: null, MaxOutputTokens: 512,
        CluePromptFile: "fr/decode-clue.md", CluePromptVersion: 2, DecodesPerClue: 5,
        Epsilon: 0.0, HarnessVersion: SoClover.Eval.Io.CalibrationFile.HarnessVersion,
        OperatorNotes: null);
}
