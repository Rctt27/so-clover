using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class LedgerWriterTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static MetricsReport Metrics(double recovery = 0.62) =>
        new("20260727-v5-gemma-a1b2c3d4", "eval/boards.dev.jsonl", "a1b2c3d4e5f6",
            40, 160,
            ValidRate: 0.94, FirstAttemptRate: 0.81, ParseFailureRate: 0.02,
            Recovery: recovery, Strict2Of2: 0.41, HalfRate: 0.33,
            BoardPositions: 0.55, BoardSolvedFirstTry: 0.12,
            ConfusionTop: [new ConfusionEntry("Infirmière", 23)],
            DecodeFailureRate: 0.01,
            ItemsCompleted: 160, ItemsExpected: 160,
            PerItemRBar: new Dictionary<(string, string), double>());

    private static LedgerEntry Entry(string runId = "20260727-v5-gemma-a1b2c3d4", double recovery = 0.62) =>
        new(new DateOnly(2026, 7, 27), runId, "eval/boards.dev.jsonl",
            "fr/board-clues-per-direction.md", 5, "gemma-4-12b-qat", "2026-07-27",
            "temp 1.0 / topP 0.95 / maxRetries 0", Metrics(recovery),
            LedgerWriter.PreCalibrationStatus, "baseline v5", "neutre", "thinking OFF, ctx 16k");

    [Fact]
    public void Creates_the_ledger_with_a_header_when_absent()
    {
        LedgerWriter.Append(_path, Entry());

        var text = File.ReadAllText(_path);
        Assert.Contains("Registre d'expériences", text);
        Assert.Contains("| date |", text);
        Assert.Contains("20260727-v5-gemma-a1b2c3d4", text);
    }

    // Le jeu réel accorde 3 tentatives avec correction par position entre chaque. La colonne n'en
    // tolère aucune : son nom doit le dire, sinon un lecteur du registre surinterprète un 0,000.
    [Fact]
    public void The_column_name_states_that_no_retry_is_tolerated()
    {
        LedgerWriter.Append(_path, Entry());

        var header = File.ReadAllLines(_path).Single(l => l.StartsWith("| date |"));

        Assert.Contains("board_solved_first_try", header);
    }

    // Le registre est le livrable le plus durable du chantier : jamais réécrit.
    [Fact]
    public void Appends_without_ever_rewriting_an_existing_row()
    {
        LedgerWriter.Append(_path, Entry("run-a"));
        var afterFirst = File.ReadAllText(_path);

        LedgerWriter.Append(_path, Entry("run-b"));
        var afterSecond = File.ReadAllText(_path);

        Assert.StartsWith(afterFirst, afterSecond);
        Assert.Equal(2, LedgerWriter.ReadRows(_path).Count);
    }

    [Fact]
    public void Appends_a_second_row_for_the_same_run_id_rather_than_replacing_it()
    {
        LedgerWriter.Append(_path, Entry(recovery: 0.62));
        LedgerWriter.Append(_path, Entry(recovery: 0.64));

        var rows = LedgerWriter.ReadRows(_path);

        Assert.Equal(2, rows.Count);
        Assert.Contains("0,620", rows[0]);
        Assert.Contains("0,640", rows[1]);
    }

    [Fact]
    public void Row_carries_every_column_the_PRD_requires()
    {
        LedgerWriter.Append(_path, Entry());

        var row = LedgerWriter.ReadRows(_path).Single();

        Assert.Contains("2026-07-27", row);
        Assert.Contains("20260727-v5-gemma-a1b2c3d4", row);
        Assert.Contains("boards.dev.jsonl", row);
        Assert.Contains("board-clues-per-direction.md", row);
        Assert.Contains("v5", row);
        Assert.Contains("gemma-4-12b-qat", row);
        Assert.Contains("temp 1.0", row);
        Assert.Contains("0,940", row);  // valid_rate
        Assert.Contains("0,620", row);  // recovery
        Assert.Contains("0,410", row);  // strict_2of2
        Assert.Contains("0,330", row);  // half_rate
        Assert.Contains("0,120", row);  // board_solved_first_try
        Assert.Contains("baseline v5", row);
        Assert.Contains("neutre", row);
        Assert.Contains("thinking OFF, ctx 16k", row);
    }

    // Dans ce cycle, aucune ligne n'est défendable : le décodeur n'a franchi que la porte
    // du plancher aléatoire.
    [Fact]
    public void Row_is_marked_pre_calibration_in_this_cycle()
    {
        LedgerWriter.Append(_path, Entry());

        Assert.Contains("pré-calibration", LedgerWriter.ReadRows(_path).Single());
    }

    // Le pipe est NEUTRALISÉ (remplacé), pas échappé en « \| » : un échappement laisserait un
    // vrai « | » dans la ligne, et le comptage naïf de colonnes ci-dessous — celui que fait
    // n'importe quel outil qui relit le registre — décalerait silencieusement.
    [Fact]
    public void Pipe_characters_in_free_text_do_not_break_the_markdown_table()
    {
        LedgerWriter.Append(_path, Entry() with
        {
            Hypothesis = "candidats | annotés",
            OperatorNotes = "note | avec | pipes",
        });

        var row = LedgerWriter.ReadRows(_path).Single();

        Assert.Equal(LedgerWriter.ColumnCount, row.Split('|').Length - 2);
        Assert.Contains("candidats", row);
    }

    [Fact]
    public void Numbers_use_a_stable_three_decimal_format()
    {
        LedgerWriter.Append(_path, Entry(recovery: 0.6));

        Assert.Contains("0,600", LedgerWriter.ReadRows(_path).Single());
    }

    [Fact]
    public void ReadRows_ignores_the_header_and_the_separator()
    {
        LedgerWriter.Append(_path, Entry());

        var rows = LedgerWriter.ReadRows(_path);

        Assert.Single(rows);
        Assert.DoesNotContain("---", rows[0]);
    }
}
