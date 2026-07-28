using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class RunMetricsTests
{
    private const string BenchHash = "a1b2c3d4e5f6";

    private static BenchContents Bench(int boards = 1) =>
        BenchGenerator.Generate(
            "dev", 20260726001, boards,
            Enumerable.Range(1, 100).Select(i => $"Mot{i:D3}").ToList(),
            "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

    private static RunManifest RunManifestStub() =>
        new("manifest", "r", "generate", DateTime.UtcNow, "eval/boards.dev.jsonl", BenchHash,
            null, 5, "PerDirection", false, "OpenAI", "u", "m", null, null, 1.0, null, null, 2,
            "Français_OFF", RunFile.HarnessVersion, null);

    private static RunAttempt Attempt(
        string boardId, Direction dir, int attempt, bool valid,
        string? failureKind = null, string? clue = "Indice") =>
        new("attempt", boardId, dir.ToString(), attempt,
            failureKind is null ? clue : null, null, null,
            valid, valid ? [] : ["ExactMatch"], failureKind,
            10, null, null, 5, "m");

    private static RunContents Run(params RunAttempt[] attempts) =>
        new(RunManifestStub(), attempts);

    private static DecodeManifest DecodeManifestStub(int decodesPerClue = 3) =>
        new("manifest", "d", DateTime.UtcNow, "r", "eval/boards.dev.jsonl", BenchHash,
            "OpenAI", "u", "m", null, null, 0.3, null, null,
            "clue.md", 1, "board.md", 1, decodesPerClue, RunFile.HarnessVersion, null);

    private static ClueDecodeLine Decode(
        string boardId, Direction dir, int index, double? r, string? failure = null) =>
        new("decode", boardId, dir.ToString(), index,
            r is null ? null : ["a", "b"], r, "1", failure, 100);

    private static BoardDecodeLine BoardDecode(string boardId, double? positions, bool? solved) =>
        new("boardDecode", boardId, positions is null ? null : new Dictionary<string, IReadOnlyList<string>>(),
            positions, solved, "1", positions is null ? "unparseable" : null, 100);

    private static DecodeContents Decoded(
        IEnumerable<ClueDecodeLine> clues, IEnumerable<BoardDecodeLine>? boards = null) =>
        new(DecodeManifestStub(), clues.ToList(), (boards ?? []).ToList());

    private static IEnumerable<ClueDecodeLine> ThreeDecodes(
        string boardId, Direction dir, params double?[] values) =>
        values.Select((r, i) => Decode(boardId, dir, i, r, r is null ? "outOfVocabulary" : null));

    // ---- N1 ------------------------------------------------------------------

    [Fact]
    public void Valid_rate_counts_directions_that_reached_a_valid_clue()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: false),
            Attempt("dev-001", Direction.Right, 1, valid: true),
            Attempt("dev-001", Direction.Bottom, 0, valid: false),
            Attempt("dev-001", Direction.Bottom, 1, valid: false),
            Attempt("dev-001", Direction.Bottom, 2, valid: false));

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        // Top et Right valides, Bottom épuisée, Left jamais tentée → 2/4.
        Assert.Equal(0.5, metrics.ValidRate);
    }

    [Fact]
    public void First_attempt_rate_counts_only_clues_obtained_without_retry()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: false),
            Attempt("dev-001", Direction.Right, 1, valid: true));

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        Assert.Equal(0.25, metrics.FirstAttemptRate);
        Assert.Equal(0.5, metrics.ValidRate);
    }

    [Fact]
    public void Parse_failure_rate_is_measured_over_attempts_not_directions()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: false, failureKind: "empty"),
            Attempt("dev-001", Direction.Top, 1, valid: false, failureKind: "unparseable"),
            Attempt("dev-001", Direction.Top, 2, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: false, failureKind: "transport"));

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        // 2 tentatives empty/unparseable sur 4 tentatives ; transport n'en fait pas partie.
        Assert.Equal(0.5, metrics.ParseFailureRate);
    }

    // ---- N2 ------------------------------------------------------------------

    [Fact]
    public void Recovery_averages_R_bar_over_every_bench_direction()
    {
        var bench = Bench();
        var run = Run(BoardGeometry.AllDirections
            .Select(d => Attempt("dev-001", d, 0, valid: true)).ToArray());

        var decodes = new List<ClueDecodeLine>();
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Top, 1.0, 1.0, 1.0));      // R̄ = 1
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Right, 0.5, 0.5, 0.5));    // R̄ = 0.5
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Bottom, 0.0, 0.0, 0.0));   // R̄ = 0
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Left, 1.0, 0.5, 0.0));     // R̄ = 0.5

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        Assert.Equal(0.5, metrics.Recovery, precision: 10);
    }

    // LE CAS DÉCISIF : une direction sans indice valide compte R̄ = 0 et reste au dénominateur.
    [Fact]
    public void A_direction_with_no_valid_clue_counts_zero_and_stays_in_the_recovery_denominator()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: false, failureKind: "empty"),
            Attempt("dev-001", Direction.Right, 1, valid: false, failureKind: "empty"),
            Attempt("dev-001", Direction.Right, 2, valid: false, failureKind: "empty"));

        var decodes = ThreeDecodes("dev-001", Direction.Top, 1.0, 1.0, 1.0).ToList();

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        // 1 direction à R̄ = 1, 3 directions sans indice valide à 0 → 0.25, pas 1.0.
        Assert.Equal(0.25, metrics.Recovery, precision: 10);
    }

    // Un décodage en échec de format est exclu du dénominateur de R̄, pas compté 0.
    [Fact]
    public void A_failed_decode_is_excluded_from_R_bar_rather_than_scored_zero()
    {
        var bench = Bench();
        var run = Run(Attempt("dev-001", Direction.Top, 0, valid: true));
        var decodes = ThreeDecodes("dev-001", Direction.Top, 1.0, 1.0, null).ToList();

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        // R̄ = moyenne de {1, 1} = 1, pas (1+1+0)/3.
        Assert.Equal(0.25, metrics.Recovery, precision: 10);
        Assert.Equal(1.0 / 3.0, metrics.DecodeFailureRate, precision: 10);
    }

    [Fact]
    public void Strict_2of2_requires_all_three_valid_decodes_at_R_equals_1()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: true));

        var decodes = new List<ClueDecodeLine>();
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Top, 1.0, 1.0, 1.0));
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Right, 1.0, 1.0, 0.5));

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        // Dénominateur = directions décodées (2), pas 4.
        Assert.Equal(0.5, metrics.Strict2Of2);
    }

    // La signature « Hôpital » : au moins 2 des 3 décodages à R = 0.5.
    [Fact]
    public void Half_rate_requires_a_dominant_R_of_one_half()
    {
        var bench = Bench();
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: true));

        var decodes = new List<ClueDecodeLine>();
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Top, 0.5, 0.5, 1.0));   // compte
        decodes.AddRange(ThreeDecodes("dev-001", Direction.Right, 0.5, 1.0, 0.0)); // ne compte pas

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        Assert.Equal(0.5, metrics.HalfRate);
    }

    [Fact]
    public void Confusion_top_ranks_non_reference_words_once_per_decode()
    {
        var bench = Bench();
        var board = bench.Boards[0];
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var distractors = BenchBoardMapper.AllWords(board).Where(w => !reference.Contains(w)).Take(2).ToList();

        var run = Run(Attempt("dev-001", Direction.Top, 0, valid: true));
        var decodes = new List<ClueDecodeLine>
        {
            new("decode", "dev-001", "Top", 0, [reference[0], distractors[0]], 0.5, "1", null, 10),
            new("decode", "dev-001", "Top", 1, [reference[0], distractors[0]], 0.5, "1", null, 10),
            new("decode", "dev-001", "Top", 2, [reference[0], distractors[1]], 0.5, "1", null, 10),
        };

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        Assert.Equal(distractors[0], metrics.ConfusionTop[0].Word);
        Assert.Equal(2, metrics.ConfusionTop[0].Count);
        Assert.DoesNotContain(metrics.ConfusionTop, e => e.Word == reference[0]);
        Assert.True(metrics.ConfusionTop.Count <= 10);
    }

    // ---- N3 ------------------------------------------------------------------

    [Fact]
    public void Board_positions_and_board_solved_are_computed_over_decoded_boards()
    {
        var bench = Bench(boards: 3);
        var run = Run();
        var boards = new[]
        {
            BoardDecode("dev-001", 1.0, true),
            BoardDecode("dev-002", 0.5, false),
            BoardDecode("dev-003", null, null),
        };

        var metrics = RunMetrics.Compute(bench, run, Decoded([], boards), maxAttempts: 3);

        Assert.Equal(0.75, metrics.BoardPositions, precision: 10);
        Assert.Equal(0.5, metrics.BoardSolved, precision: 10);
    }

    // ---- Santé ---------------------------------------------------------------

    [Fact]
    public void Items_completed_over_expected_makes_a_partial_run_visibly_suspect()
    {
        var bench = Bench(boards: 2);
        var run = Run(
            Attempt("dev-001", Direction.Top, 0, valid: true),
            Attempt("dev-001", Direction.Right, 0, valid: false),
            Attempt("dev-001", Direction.Right, 1, valid: false),
            Attempt("dev-001", Direction.Right, 2, valid: false));

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        Assert.Equal(2, metrics.ItemsCompleted);
        Assert.Equal(8, metrics.ItemsExpected);
    }

    // ---- Cas limites ---------------------------------------------------------

    [Fact]
    public void An_empty_run_yields_zeros_rather_than_NaN()
    {
        var metrics = RunMetrics.Compute(Bench(), Run(), decoded: null, maxAttempts: 3);

        Assert.Equal(0.0, metrics.ValidRate);
        Assert.Equal(0.0, metrics.FirstAttemptRate);
        Assert.Equal(0.0, metrics.ParseFailureRate);
        Assert.Equal(0.0, metrics.Recovery);
        Assert.Equal(0.0, metrics.Strict2Of2);
        Assert.Equal(0.0, metrics.HalfRate);
        Assert.Equal(0.0, metrics.BoardPositions);
        Assert.Equal(0.0, metrics.BoardSolved);
        Assert.Equal(0.0, metrics.DecodeFailureRate);
        Assert.Empty(metrics.ConfusionTop);
        Assert.Equal(0, metrics.ItemsCompleted);
        Assert.Equal(4, metrics.ItemsExpected);
    }

    [Fact]
    public void A_fully_invalid_run_scores_zero_recovery_over_the_full_denominator()
    {
        var bench = Bench();
        var run = Run(BoardGeometry.AllDirections
            .SelectMany(d => Enumerable.Range(0, 3).Select(i => Attempt("dev-001", d, i, valid: false)))
            .ToArray());

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        Assert.Equal(0.0, metrics.ValidRate);
        Assert.Equal(0.0, metrics.Recovery);
        Assert.Equal(4, metrics.ItemsCompleted);
    }

    [Fact]
    public void Missing_decodes_do_not_break_the_report()
    {
        var bench = Bench();
        var run = Run(BoardGeometry.AllDirections
            .Select(d => Attempt("dev-001", d, 0, valid: true)).ToArray());

        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        Assert.Equal(1.0, metrics.ValidRate);
        Assert.Equal(0.0, metrics.Recovery);
        Assert.Equal(0.0, metrics.Strict2Of2);
    }

    [Fact]
    public void Per_item_R_bar_is_exposed_for_the_paired_comparison()
    {
        var bench = Bench();
        var run = Run(Attempt("dev-001", Direction.Top, 0, valid: true));
        var decodes = ThreeDecodes("dev-001", Direction.Top, 1.0, 0.5, 0.5).ToList();

        var metrics = RunMetrics.Compute(bench, run, Decoded(decodes), maxAttempts: 3);

        Assert.Equal(4, metrics.PerItemRBar.Count);
        Assert.Equal(2.0 / 3.0, metrics.PerItemRBar[("dev-001", "Top")], precision: 10);
        Assert.Equal(0.0, metrics.PerItemRBar[("dev-001", "Left")]);
    }

    // PerItemRBar est un dictionnaire à clé tuple : System.Text.Json ne sait pas l'écrire.
    // Ce test garde le [JsonIgnore] qui rend le rapport sérialisable.
    [Fact]
    public void The_report_serializes_without_the_per_item_detail()
    {
        var bench = Bench();
        var run = Run(Attempt("dev-001", Direction.Top, 0, valid: true));
        var metrics = RunMetrics.Compute(bench, run, decoded: null, maxAttempts: 3);

        var json = EvalJson.Serialize(metrics);

        Assert.Contains("\"recovery\"", json);
        Assert.DoesNotContain("perItemRBar", json);
    }
}
