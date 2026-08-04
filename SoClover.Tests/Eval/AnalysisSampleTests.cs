using SoClover.Domain;
using SoClover.Eval.Analysis;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class AnalysisSampleTests
{
    private static SampleItem Item(
        string boardId = "dev-007", string direction = "Top", string clue = "Pédiatre",
        string autoMode = FailureModes.M2, string? humanMode = null, double rBar = 0.5) => new(
        BoardId: boardId,
        Direction: direction,
        Clue: clue,
        ReferenceWords: ["Chirurgien", "Enfant"],
        BoardWords: ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
                     "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont"],
        DecodeLines: ["Chirurgien + Miel   (R = 0,5)", "Chirurgien + Route   (R = 0,5)",
                      "— (échec de format : unparseable)"],
        RBar: rBar,
        AutoMode: autoMode,
        HumanMode: humanMode);

    // Un fichier LISIBLE PAR UN HUMAIN, pas un JSON : c'est la condition pour que les 20 items
    // soient réellement lus.
    [Fact]
    public void The_rendered_sample_shows_everything_needed_to_judge_an_item()
    {
        var markdown = AnalysisSample.Render("run-x", 4242, [Item()]);

        Assert.Contains("run-x", markdown);
        Assert.Contains("4242", markdown);
        Assert.Contains("Pédiatre", markdown);
        Assert.Contains("Chirurgien", markdown);
        Assert.Contains("Montagne", markdown);          // les 16 mots
        Assert.Contains("Chirurgien + Miel", markdown); // les décodages obtenus
        Assert.Contains(FailureModes.M2, markdown);     // l'étiquette proposée
        Assert.Contains("étiquette humaine :", markdown);
    }

    [Fact]
    public void The_rendered_sample_lists_the_closed_vocabulary_of_modes()
    {
        var markdown = AnalysisSample.Render("run-x", 1, [Item()]);

        Assert.All(FailureModes.All, m => Assert.Contains(m, markdown));
    }

    [Fact]
    public void Round_trips_through_render_and_parse()
    {
        var items = new[]
        {
            Item(boardId: "dev-007", direction: "Top", clue: "Pédiatre", autoMode: FailureModes.M2),
            Item(boardId: "dev-012", direction: "Left", clue: "Mer", autoMode: FailureModes.M1),
        };

        var parsed = AnalysisSample.Parse(AnalysisSample.Render("run-x", 7, items));

        Assert.Equal(2, parsed.Count);
        Assert.Equal("dev-012", parsed[1].BoardId);
        Assert.Equal("Left", parsed[1].Direction);
        Assert.Equal("Mer", parsed[1].Clue);
        Assert.Equal(FailureModes.M1, parsed[1].AutoMode);
        Assert.Null(parsed[1].HumanMode);
    }

    [Fact]
    public void Parses_the_human_label_once_the_operator_has_filled_it()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M3");

        Assert.Equal(FailureModes.M3, AnalysisSample.Parse(markdown).Single().HumanMode);
    }

    // M5 est posé À LA MAIN sur l'échantillon lu : c'est la seule voie par laquelle il entre.
    [Fact]
    public void A_hand_written_M5_is_accepted()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M5");

        Assert.Equal(FailureModes.M5, AnalysisSample.Parse(markdown).Single().HumanMode);
    }

    [Fact]
    public void Refuses_a_human_label_outside_the_closed_vocabulary()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M9");

        var ex = Assert.Throws<ArgumentException>(() => AnalysisSample.Parse(markdown));
        Assert.Contains("M9", ex.Message);
    }

    // ---- Matrice de confusion --------------------------------------------------

    [Fact]
    public void Review_reports_the_agreement_between_the_automatic_and_the_human_labels()
    {
        var items = new[]
        {
            Item(autoMode: FailureModes.M2, humanMode: FailureModes.M2),
            Item(autoMode: FailureModes.M2, humanMode: FailureModes.M2, boardId: "dev-001"),
            Item(autoMode: FailureModes.M1, humanMode: FailureModes.M3, boardId: "dev-002"),
            Item(autoMode: FailureModes.M4, humanMode: FailureModes.M4, boardId: "dev-003"),
        };

        var matrix = AnalysisSample.Review(items);

        Assert.Equal(4, matrix.Total);
        Assert.Equal(0.75, matrix.Agreement, precision: 10);
        Assert.Equal(2, matrix.Rows.Single(r => r.AutoMode == FailureModes.M2
                                                && r.HumanMode == FailureModes.M2).Count);
        Assert.Equal(1, matrix.Rows.Single(r => r.AutoMode == FailureModes.M1
                                                && r.HumanMode == FailureModes.M3).Count);
    }

    [Fact]
    public void The_review_threshold_is_seventy_percent()
    {
        Assert.Equal(0.70, AnalysisSample.ReviewAgreementThreshold);
    }

    // Une relecture partielle n'est pas une relecture : le refus nomme le nombre de lignes vides.
    [Fact]
    public void Review_refuses_a_sample_whose_human_labels_are_missing()
    {
        var items = new[]
        {
            Item(humanMode: FailureModes.M2),
            Item(humanMode: null, boardId: "dev-001"),
            Item(humanMode: null, boardId: "dev-002"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => AnalysisSample.Review(items));

        Assert.Contains("2", ex.Message);
    }

    // ---- Tirage ----------------------------------------------------------------

    [Fact]
    public void The_draw_is_reproducible_for_a_fixed_seed()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var first = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 99);
        var second = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 99);

        Assert.Equal(
            first.Select(i => (i.BoardId, i.Direction)),
            second.Select(i => (i.BoardId, i.Direction)));
    }

    [Fact]
    public void A_different_seed_draws_a_different_sample()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var a = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 1);
        var b = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 2);

        Assert.NotEqual(
            a.Select(i => (i.BoardId, i.Direction)).ToList(),
            b.Select(i => (i.BoardId, i.Direction)).ToList());
    }

    // On tire parmi les ÉCHECS : lire 20 réussites n'apprendrait rien sur les modes d'échec.
    [Fact]
    public void The_draw_only_picks_failing_directions()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var sample = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 10, seed: 5);

        Assert.All(sample, i => Assert.True(i.RBar < FailureTaxonomy.SuccessThreshold));
        Assert.DoesNotContain(sample, i => i.AutoMode == FailureModes.M0);
    }

    [Fact]
    public void The_draw_is_capped_by_the_number_of_available_failures()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var sample = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 10_000, seed: 5);

        Assert.Equal(taxonomy.Labels.Count(l => l.Mode != FailureModes.M0), sample.Count);
    }

    // I1 : une direction D6 (aucun décodage exploitable) n'est pas un échec sémantique — le
    // vocabulaire fermé n'a aucun code pour « échec de format du décodeur ». La faire entrer dans
    // le tirage tirerait vers le bas l'accord auto ↔ humain. La taille demandée dépasse largement
    // le nombre de candidats réels : si Bottom pouvait être tiré, il le serait forcément ici.
    [Fact]
    public void The_draw_never_picks_a_direction_without_any_exploitable_decode()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var run = HumanTestData.Run(bench, "run-d6", "clue");
        var board = bench.Boards[0];
        var directions = BoardGeometry.AllDirections;

        var clueDecodes = new List<ClueDecodeLine>();

        // Top : M4 (rBar = 0, aucun mot commun entre les deux décodages).
        clueDecodes.Add(new("decode", board.BoardId, directions[0].ToString(), 0,
            ["faux1", "faux2"], 0.0, "1", null, 100));
        clueDecodes.Add(new("decode", board.BoardId, directions[0].ToString(), 1,
            ["faux3", "faux4"], 0.0, "1", null, 100));

        // Right : M1 (dispersé).
        var refsRight = BenchBoardMapper.ReferenceWords(board, directions[1]);
        clueDecodes.Add(new("decode", board.BoardId, directions[1].ToString(), 0,
            [refsRight[0], "y1"], 0.5, "1", null, 100));
        clueDecodes.Add(new("decode", board.BoardId, directions[1].ToString(), 1,
            ["y2", "y3"], 0.0, "1", null, 100));
        clueDecodes.Add(new("decode", board.BoardId, directions[1].ToString(), 2,
            ["y4", "y5"], 0.0, "1", null, 100));

        // Left : M0 (réussite — jamais tiré non plus, mais pour une autre raison).
        var refsLeft = BenchBoardMapper.ReferenceWords(board, directions[2]);
        clueDecodes.Add(new("decode", board.BoardId, directions[2].ToString(), 0,
            refsLeft.ToList(), 1.0, "1", null, 100));

        // Bottom : D6, aucun décodage exploitable.
        clueDecodes.Add(new("decode", board.BoardId, directions[3].ToString(), 0,
            null, null, "1", "unparseable", 100));

        var manifest = new SoClover.Eval.Decoder.DecodeManifest(
            "manifest", "run-d6-decoded", DateTime.UtcNow, "run-d6",
            "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
            1.0, null, null, "test.md", 1, "test.md", 1, 3, 1, null);

        var decoded = new DecodeContents(
            manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());
        var taxonomy = FailureTaxonomy.Compute(bench, run, decoded);

        var sample = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 10, seed: 7);

        Assert.DoesNotContain(sample, i => i.Direction == directions[3].ToString());
        Assert.Equal(2, sample.Count);
    }
}

/// <summary>
/// Fabrique un banc de 5 boards (<see cref="HumanTestData.Bench"/>), un run synthétique
/// (<see cref="HumanTestData.Run"/>) et un <see cref="DecodeContents"/> où la moitié des
/// 20 directions réussit (R = 1) et l'autre moitié échoue (R = 0, mots faux tous distincts
/// entre les deux décodages d'une même direction, donc M4), puis calcule la taxonomie dessus.
/// </summary>
internal static class AnalysisFixture
{
    public static (BenchContents Bench, SoClover.Eval.Runner.RunContents Run, DecodeContents Decoded,
        TaxonomyReport Taxonomy) Build()
    {
        var bench = HumanTestData.Bench(5);
        var run = HumanTestData.Run(bench, "run-x", "clue");

        var clueDecodes = new List<ClueDecodeLine>();
        var wrongCounter = 0;
        var index = 0;

        foreach (var board in bench.Boards)
        {
            foreach (var direction in BoardGeometry.AllDirections)
            {
                var reference = BenchBoardMapper.ReferenceWords(board, direction);
                var succeeds = index % 2 == 0;

                if (succeeds)
                {
                    clueDecodes.Add(new ClueDecodeLine(
                        Kind: "decode", BoardId: board.BoardId, Direction: direction.ToString(),
                        DecodeIndex: 0, Picked: reference, R: 1.0,
                        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100));
                }
                else
                {
                    clueDecodes.Add(new ClueDecodeLine(
                        Kind: "decode", BoardId: board.BoardId, Direction: direction.ToString(),
                        DecodeIndex: 0,
                        Picked: [$"faux{wrongCounter++}", $"faux{wrongCounter++}"], R: 0.0,
                        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100));
                    clueDecodes.Add(new ClueDecodeLine(
                        Kind: "decode", BoardId: board.BoardId, Direction: direction.ToString(),
                        DecodeIndex: 1,
                        Picked: [$"faux{wrongCounter++}", $"faux{wrongCounter++}"], R: 0.0,
                        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100));
                }

                index++;
            }
        }

        var manifest = new DecodeManifest(
            Kind: "manifest", DecodeRunId: "run-x-decoded", CreatedAtUtc: DateTime.UtcNow,
            GeneratorRunId: run.Manifest.RunId, BenchFile: "eval/boards.dev.jsonl",
            BenchHash: bench.Manifest.BenchHash, Provider: "test", BaseUrl: "", ModelId: "test",
            ModelSnapshotDate: null, ProviderModelListHash: null, Temperature: 1.0, TopP: null,
            MaxOutputTokens: null, CluePromptFile: "test.md", CluePromptVersion: 1,
            BoardPromptFile: "test.md", BoardPromptVersion: 1, DecodesPerClue: 3,
            HarnessVersion: SoClover.Eval.Io.RunFile.HarnessVersion, OperatorNotes: null);

        var decoded = new DecodeContents(
            manifest, clueDecodes.AsReadOnly(), new List<BoardDecodeLine>().AsReadOnly());

        var taxonomy = FailureTaxonomy.Compute(bench, run, decoded);

        return (bench, run, decoded, taxonomy);
    }
}
