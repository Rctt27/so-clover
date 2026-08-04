using SoClover.Domain;
using SoClover.Eval.Analysis;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class FailureTaxonomyTests
{
    private static readonly string[] Reference = ["Chirurgien", "Enfant"];

    private static ClueDecodeLine Decode(int index, params string[] picked) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: picked, R: picked.Count(Reference.Contains) / 2.0,
        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100);

    private static ClueDecodeLine Failed(int index) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: null, R: null, ShuffleSeed: "1", DecodeFailureKind: "unparseable", LatencyMs: 100);

    private static string Mode(params ClueDecodeLine[] decodes) =>
        FailureTaxonomy.LabelDirection(Reference, decodes).Mode;

    // M0 n'est pas un mode d'échec — mais il faut un dénominateur honnête.
    [Fact]
    public void M0_is_a_direction_that_succeeded()
    {
        Assert.Equal(FailureModes.M0, Mode(
            Decode(0, "Chirurgien", "Enfant"),
            Decode(1, "Chirurgien", "Enfant"),
            Decode(2, "Chirurgien", "Miel")));
    }

    [Fact]
    public void The_success_threshold_is_three_quarters()
    {
        Assert.Equal(0.75, FailureTaxonomy.SuccessThreshold);
    }

    // M2, la signature « Hôpital » : l'indice n'attrape qu'une face, TOUJOURS LA MÊME.
    [Fact]
    public void M2_needs_two_half_decodes_missing_the_same_face()
    {
        Assert.Equal(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Route"),
            Decode(2, "Chirurgien", "Vent")));
    }

    [Fact]
    public void Two_half_decodes_missing_different_faces_are_not_M2()
    {
        Assert.NotEqual(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route")));
    }

    // M3 : signature CONCENTRÉE — un même distracteur revient.
    [Fact]
    public void M3_is_a_single_distractor_chosen_in_at_least_two_decodes()
    {
        Assert.Equal(FailureModes.M3, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Miel", "Ciel")));
    }

    // M1 : signature DISPERSÉE — au moins 4 mots non-référence distincts, R̄ ≤ 0,5 mais NON NUL
    // (à R̄ = 0, c'est M4 qui l'emporte : voir D9).
    [Fact]
    public void M1_is_at_least_four_distinct_wrong_words_and_a_low_RBar()
    {
        Assert.Equal(FailureModes.M1, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Route", "Vent"),
            Decode(2, "Ciel", "Pont")));
    }

    // M4 : R̄ = 0 ET aucun mot commun d'un décodage à l'autre.
    [Fact]
    public void M4_is_zero_recovery_with_no_word_shared_between_decodes()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel"),
            Decode(2, "Pont", "Sable")));
    }

    // D5 : avec un seul décodage, « aucun mot commun » est vide de sens — l'intersection d'un
    // singleton avec lui-même n'est jamais vide. Sans cette garde, M4 absorberait les
    // directions mal décodées.
    [Fact]
    public void A_single_scored_decode_cannot_be_M4()
    {
        Assert.Equal(FailureModes.Unclassified, Mode(Decode(0, "Miel", "Route")));
    }

    // L'ORDRE DE PRIORITÉ, sur un item qui satisfait plusieurs signatures à la fois.
    [Fact]
    public void M2_wins_over_M3_when_both_signatures_hold()
    {
        // Deux décodages à R = 0,5 manquant « Enfant », ET « Miel » choisi deux fois.
        var mode = Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Miel"));

        Assert.Equal(FailureModes.M2, mode);
    }

    [Fact]
    public void M3_wins_over_M1_when_both_signatures_hold()
    {
        // « Miel » concentré (2 fois) ET 4 mots faux distincts.
        var mode = Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Ciel", "Pont"));

        Assert.Equal(FailureModes.M3, mode);
    }

    // D9 : M4 s'évalue AVANT M1. Sous l'ordre littéral du design (M1 puis M4), M4 serait
    // STRUCTURELLEMENT INATTEIGNABLE — R̄ = 0 implique 2 mots faux par décodage, et une
    // intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux distincts, donc M1.
    [Fact]
    public void M4_wins_over_M1_because_zero_recovery_is_the_stronger_signature()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel")));
    }

    [Fact]
    public void The_priority_order_is_exposed_and_documented()
    {
        Assert.Equal(
            [FailureModes.M2, FailureModes.M3, FailureModes.M4, FailureModes.M1],
            FailureTaxonomy.PriorityOrder);
    }

    // Une taxonomie qui classe 100 % des items est une taxonomie qui triche.
    [Fact]
    public void An_item_matching_no_signature_is_left_unclassified()
    {
        // R̄ = 0,667 : sous le seuil de réussite, au-dessus de 0,5 (donc pas M1), non nul (pas M4),
        // deux faces manquées différentes (pas M2), aucun distracteur répété (pas M3).
        Assert.Equal(FailureModes.Unclassified, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route"),
            Decode(2, "Chirurgien", "Enfant")));
    }

    // D6 : une direction sans aucun décodage exploitable n'est pas un mode d'échec sémantique.
    [Fact]
    public void A_direction_without_a_single_scored_decode_is_unclassified_and_counted_apart()
    {
        var (mode, rbar, scored) = FailureTaxonomy.LabelDirection(Reference, [Failed(0), Failed(1)]);

        Assert.Equal(FailureModes.Unclassified, mode);
        Assert.Equal(0.0, rbar);
        Assert.Equal(0, scored);
    }

    // M5 reste MANUEL, et c'est une décision, pas un oubli : le harnais n'embarque aucune
    // ressource de fréquence lexicale, et un proxy inventé donnerait une fausse rigueur.
    [Fact]
    public void M5_is_never_produced_automatically()
    {
        var everyShape = new[]
        {
            Mode(Decode(0, "Chirurgien", "Enfant")),
            Mode(Decode(0, "Chirurgien", "Miel"), Decode(1, "Chirurgien", "Route")),
            Mode(Decode(0, "Miel", "Route"), Decode(1, "Vent", "Ciel")),
        };

        Assert.DoesNotContain(FailureModes.M5, everyShape);
    }

    [Fact]
    public void M5_still_has_a_label_for_the_hand_written_sample()
    {
        Assert.False(string.IsNullOrWhiteSpace(FailureModes.Label(FailureModes.M5)));
    }

    // Tests de Compute() : couverture de la logique d'agrégation au niveau board/run

    [Fact]
    public void Compute_basic_report_structure()
    {
        // Test minimal : un banc simple avec des décodages trivaux (tous M0).
        // Vérifier que Compute() retourne un rapport avec les champs attendus.
        var bench = HumanTestData.Bench(boardCount: 2);
        var run = HumanTestData.Run(bench, "test-run-min", "prefix");

        // Créer des décodages simples : un succès (M0) par direction
        var clueDecodes = new List<ClueDecodeLine>();
        foreach (var board in bench.Boards)
        {
            foreach (var direction in BoardGeometry.AllDirections)
            {
                var refs = BenchBoardMapper.ReferenceWords(board, direction);
                clueDecodes.Add(new("decode", board.BoardId, direction.ToString(), 0,
                    refs.ToList(), 1.0, "1", null, 100));
            }
        }

        var manifest = new DecodeManifest(
            "manifest", "test-decode-min", DateTime.UtcNow, "test-run-min",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        // Vérifications de structure
        Assert.NotNull(report);
        Assert.Equal(run.Manifest.RunId, report.RunId);
        Assert.Equal(8, report.DirectionCount); // 2 boards × 4 directions
        Assert.Equal(8, report.ScorableDirectionCount);
        Assert.Equal(0, report.UnscorableDirectionCount);
        Assert.Equal(2, report.BoardCount);
    }

    [Fact]
    public void Compute_ActionJustified_above_five_percent()
    {
        // Test : ActionJustified vrai quand part > 5%
        var bench = HumanTestData.Bench(boardCount: 1);
        var run = HumanTestData.Run(bench, "test-run-5pct", "prefix");
        var board = bench.Boards[0];

        var clueDecodes = new List<ClueDecodeLine>();
        var directions = BoardGeometry.AllDirections.Select(d => d.ToString()).ToList();

        // Direction 0 : M1 (dispersé) — pour que ce compte > 5% avec 4 directions
        var refs0 = BenchBoardMapper.ReferenceWords(board, BoardGeometry.AllDirections[0]);
        clueDecodes.Add(new("decode", board.BoardId, directions[0], 0,
            new string[] { refs0[0], "x1" }, 0.5, "1", null, 100));
        clueDecodes.Add(new("decode", board.BoardId, directions[0], 1,
            new string[] { "x2", "x3" }, 0.0, "1", null, 100));
        clueDecodes.Add(new("decode", board.BoardId, directions[0], 2,
            new string[] { "x4", "x5" }, 0.0, "1", null, 100));

        // Directions 1-3 : M0 (succès)
        for (var i = 1; i < 4; i++)
        {
            var refs = BenchBoardMapper.ReferenceWords(board, BoardGeometry.AllDirections[i]);
            clueDecodes.Add(new("decode", board.BoardId, directions[i], 0,
                refs.ToList(), 1.0, "1", null, 100));
        }

        var manifest = new DecodeManifest(
            "manifest", "test-decode-5pct", DateTime.UtcNow, "test-run-5pct",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        // M1 → 1/4 = 25% > 5%
        var m1 = report.Distribution.Single(d => d.Mode == FailureModes.M1);
        Assert.True(m1.ActionJustified);
    }

    [Fact]
    public void Compute_ActionJustified_false_strictly_below_five_percent()
    {
        // Test : ActionJustified faux quand part < 5%
        var bench = HumanTestData.Bench(boardCount: 10); // 10 × 4 = 40 directions (< 5% = < 2 directions)
        var run = HumanTestData.Run(bench, "test-run-below5", "prefix");

        var clueDecodes = new List<ClueDecodeLine>();
        var allDirections = BoardGeometry.AllDirections.Select(d => d.ToString()).ToList();
        var isFirst = true;

        foreach (var board in bench.Boards)
        {
            for (var i = 0; i < 4; i++)
            {
                var direction = allDirections[i];
                var refs = BenchBoardMapper.ReferenceWords(board, BoardGeometry.AllDirections[i]);

                if (isFirst)
                {
                    // M1 : 1 direction parmi 40 = 2.5% < 5%
                    clueDecodes.Add(new("decode", board.BoardId, direction, 0,
                        new string[] { refs[0], "x1" }, 0.5, "1", null, 100));
                    clueDecodes.Add(new("decode", board.BoardId, direction, 1,
                        new string[] { "x2", "x3" }, 0.0, "1", null, 100));
                    clueDecodes.Add(new("decode", board.BoardId, direction, 2,
                        new string[] { "x4", "x5" }, 0.0, "1", null, 100));
                    isFirst = false;
                }
                else
                {
                    // M0
                    clueDecodes.Add(new("decode", board.BoardId, direction, 0,
                        refs.ToList(), 1.0, "1", null, 100));
                }
            }
        }

        var manifest = new DecodeManifest(
            "manifest", "test-decode-below5", DateTime.UtcNow, "test-run-below5",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        // M1 → 1/40 = 2.5% < 5% → ActionJustified = false
        var m1 = report.Distribution.Single(d => d.Mode == FailureModes.M1);
        Assert.Equal(1, m1.Count);
        Assert.False(m1.ActionJustified);
    }

    [Fact]
    public void Compute_unscorables_counted_separately()
    {
        // D6 : une direction sans décodage exploitable est comptée à part.
        var bench = HumanTestData.Bench(boardCount: 1);
        var run = HumanTestData.Run(bench, "test-run-unscore", "prefix");
        var board = bench.Boards[0];

        var clueDecodes = new List<ClueDecodeLine>();
        var directions = BoardGeometry.AllDirections.Select(d => d.ToString()).ToList();

        // Top, Right, Left : M0
        for (var i = 0; i < 3; i++)
        {
            var refs = BenchBoardMapper.ReferenceWords(board, BoardGeometry.AllDirections[i]);
            clueDecodes.Add(new("decode", board.BoardId, directions[i], 0,
                refs.ToList(), 1.0, "1", null, 100));
        }

        // Bottom : inexploritable (DecodFailureKind non-null)
        clueDecodes.Add(new("decode", board.BoardId, directions[3], 0,
            null, null, "1", "unparseable", 100));

        var manifest = new DecodeManifest(
            "manifest", "test-decode-unscore", DateTime.UtcNow, "test-run-unscore",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        Assert.Equal(4, report.DirectionCount);
        Assert.Equal(3, report.ScorableDirectionCount);
        Assert.Equal(1, report.UnscorableDirectionCount);
    }

    // I1 : le dénominateur des parts est le nombre de directions EXPLOITABLES, jamais le total.
    // 44 directions : 4 D6 (inexploitables), 2 M1, 38 M0.
    // Naïvement (dénominateur = 44) : 2/44 = 0,0454... < 5 % → PAS d'intervention.
    // Corrigé (dénominateur = 40 exploitables) : 2/40 = 0,05 → intervention justifiée. C'est
    // exactement le cas que le finding décrit : un mode réel dilué sous le seuil par des
    // directions qui n'ont rien à voir avec un mode d'échec sémantique.
    [Fact]
    public void Compute_mode_share_is_computed_on_exploitable_directions_only()
    {
        var bench = HumanTestData.Bench(boardCount: 11);
        var run = HumanTestData.Run(bench, "test-run-denom", "prefix");

        var clueDecodes = new List<ClueDecodeLine>();
        var allDirections = BoardGeometry.AllDirections.Select(d => d.ToString()).ToList();

        var boardIndex = 0;
        foreach (var board in bench.Boards)
        {
            for (var i = 0; i < 4; i++)
            {
                var direction = allDirections[i];
                var refs = BenchBoardMapper.ReferenceWords(board, BoardGeometry.AllDirections[i]);
                var globalIndex = boardIndex * 4 + i;

                if (globalIndex < 4)
                {
                    // D6 : aucun décodage exploitable.
                    clueDecodes.Add(new("decode", board.BoardId, direction, 0,
                        null, null, "1", "unparseable", 100));
                }
                else if (globalIndex < 6)
                {
                    // M1 : dispersé.
                    clueDecodes.Add(new("decode", board.BoardId, direction, 0,
                        new[] { refs[0], "x1" }, 0.5, "1", null, 100));
                    clueDecodes.Add(new("decode", board.BoardId, direction, 1,
                        new[] { "x2", "x3" }, 0.0, "1", null, 100));
                    clueDecodes.Add(new("decode", board.BoardId, direction, 2,
                        new[] { "x4", "x5" }, 0.0, "1", null, 100));
                }
                else
                {
                    clueDecodes.Add(new("decode", board.BoardId, direction, 0,
                        refs.ToList(), 1.0, "1", null, 100));
                }
            }

            boardIndex++;
        }

        var manifest = new DecodeManifest(
            "manifest", "test-decode-denom", DateTime.UtcNow, "test-run-denom",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        Assert.Equal(44, report.DirectionCount);
        Assert.Equal(40, report.ScorableDirectionCount);
        Assert.Equal(4, report.UnscorableDirectionCount);

        var m1 = report.Distribution.Single(d => d.Mode == FailureModes.M1);
        Assert.Equal(2, m1.Count);
        Assert.Equal(0.05, m1.Share, precision: 10);
        Assert.True(m1.ActionJustified);

        // Le dénominateur naïf (44) aurait donné 0,0454... < 5 % : c'est bien la correction du
        // dénominateur, pas un hasard de construction, qui fait franchir le seuil.
        Assert.NotEqual(2 / 44.0, m1.Share, precision: 10);
    }

    [Fact]
    public void Compute_M6_boards_above_thresholds()
    {
        // M6 : board franchit M6MinBoardMean (0.50) et M6MinGap (0.20).
        var bench = HumanTestData.Bench(boardCount: 1);
        var run = HumanTestData.Run(bench, "test-run-m6in", "prefix");
        var board = bench.Boards[0];

        // Toutes les directions : M0 (R̄ = 1.0 par direction)
        // → Moyenne board = 1.0 ≥ 0.50 ✓
        var clueDecodes = new List<ClueDecodeLine>();
        foreach (var direction in BoardGeometry.AllDirections)
        {
            var refs = BenchBoardMapper.ReferenceWords(board, direction);
            clueDecodes.Add(new("decode", board.BoardId, direction.ToString(), 0,
                refs.ToList(), 1.0, "1", null, 100));
        }

        // BoardDecode : boardPositions = 0.75
        // → gap = 1.0 - 0.75 = 0.25 ≥ 0.20 ✓
        var boardDecodes = new List<BoardDecodeLine>
        {
            new("board", board.BoardId, null, 0.75, true, "1", null, 100),
        };

        var manifest = new DecodeManifest(
            "manifest", "test-decode-m6in", DateTime.UtcNow, "test-run-m6in",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), boardDecodes.AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        Assert.Equal(1, report.M6BoardCount);
        Assert.Contains(board.BoardId, report.M6Boards);
        Assert.Equal(1.0, report.M6Share);
    }

    [Fact]
    public void Compute_M6_boards_excluded_below_thresholds()
    {
        // M6 : board ne franchit PAS le seuil M6MinGap.
        var bench = HumanTestData.Bench(boardCount: 1);
        var run = HumanTestData.Run(bench, "test-run-m6out", "prefix");
        var board = bench.Boards[0];

        // Toutes les directions : M0
        var clueDecodes = new List<ClueDecodeLine>();
        foreach (var direction in BoardGeometry.AllDirections)
        {
            var refs = BenchBoardMapper.ReferenceWords(board, direction);
            clueDecodes.Add(new("decode", board.BoardId, direction.ToString(), 0,
                refs.ToList(), 1.0, "1", null, 100));
        }

        // BoardDecode : boardPositions = 0.95
        // → gap = 1.0 - 0.95 = 0.05 < 0.20 ✗
        var boardDecodes = new List<BoardDecodeLine>
        {
            new("board", board.BoardId, null, 0.95, true, "1", null, 100),
        };

        var manifest = new DecodeManifest(
            "manifest", "test-decode-m6out", DateTime.UtcNow, "test-run-m6out",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 1, 1, null);

        var decoded = new DecodeContents(manifest, clueDecodes.AsReadOnly(), boardDecodes.AsReadOnly());
        var report = FailureTaxonomy.Compute(bench, run, decoded);

        Assert.Equal(0, report.M6BoardCount);
        Assert.Empty(report.M6Boards);
        Assert.Equal(0.0, report.M6Share);
    }
}
