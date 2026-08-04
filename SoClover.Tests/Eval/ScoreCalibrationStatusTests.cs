using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class ScoreCalibrationStatusTests
{
    private const string Fingerprint = "3f2a91c4e0d1";

    private static DecodeContents Decoded(int? cluePromptVersion = 2) =>
        new(DecoderFingerprintTests.Manifest(cluePromptVersion: cluePromptVersion), [], []);

    private static CalibrationReport Report(bool allPassed = true, string? fingerprint = null) => new(
        CalibrationId: "20260805-x",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        DecoderFingerprint: fingerprint ?? DecoderFingerprint.FromManifest(Decoded().Manifest),
        ModelId: "qwen/qwen3-8b",
        CluePromptVersion: 2,
        DecodesPerClue: 5,
        Epsilon: 0.0,
        Agreement: null!,
        SaturationRecovery: 0.88,
        FloorRecovery: 0.11,
        Gates: [],
        AllGatesPassed: allPassed,
        Verdict: allPassed ? CalibrationGates.ValidatedLabel : CalibrationGates.RejectedLabel,
        Reasons: [],
        IntraJudgeAgreement: 0.9,
        DuplicatePairCount: 10,
        IntraJudgeBelowGate: false,
        Position1Suspect: false,
        AnchorSuspect: false,
        OperatorNotes: null);

    // Sans le drapeau, le comportement actuel est INCHANGÉ.
    [Fact]
    public void Without_the_flag_the_status_stays_pre_calibration()
    {
        Assert.Equal(
            LedgerWriter.PreCalibrationStatus,
            ScoreCommand.ResolveStatus(calibration: null, Decoded(), "—"));
    }

    [Fact]
    public void With_four_passed_gates_and_a_matching_fingerprint_the_status_becomes_calibrated()
    {
        var status = ScoreCommand.ResolveStatus(Report(), Decoded(), "calibration.json");

        Assert.StartsWith(LedgerWriter.CalibratedStatus, status);
    }

    // La ligne doit PORTER son empreinte : c'est ce qui rendra lisible, dans six mois, l'effet
    // du passage de decode-clue v1 à v2. Et les 18 colonnes restent 18.
    [Fact]
    public void The_calibrated_status_carries_the_decoder_fingerprint()
    {
        var expected = DecoderFingerprint.FromManifest(Decoded().Manifest);

        Assert.Contains(expected, ScoreCommand.ResolveStatus(Report(), Decoded(), "calibration.json"));
    }

    // Refus BRUYANT, jamais de repli silencieux en pré-calibration.
    [Fact]
    public void Refuses_loudly_when_a_gate_has_not_been_passed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(allPassed: false), Decoded(), "calibration.json"));

        Assert.Contains("portes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(LedgerWriter.PreCalibrationStatus, ex.Message);
    }

    [Fact]
    public void Refuses_when_the_run_was_decoded_by_another_decoder()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(), Decoded(cluePromptVersion: 1), "calibration.json"));

        Assert.Contains("empreinte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_a_run_that_was_never_decoded()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(), decoded: null, "calibration.json"));

        Assert.Contains("décod", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Registre --------------------------------------------------------------

    [Fact]
    public void A_calibrated_row_still_carries_exactly_eighteen_columns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.CalibratedStatusFor(Fingerprint)));

            var row = LedgerWriter.ReadRows(path).Single();

            Assert.Equal(LedgerWriter.ColumnCount, row.Split('|').Length - 2);
            Assert.Contains("calibré", row);
            Assert.Contains(Fingerprint, row);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // Les lignes existantes ne sont JAMAIS réécrites : les runs antérieurs obtiennent une ligne
    // `calibré` par re-score, et les deux lignes coexistent.
    [Fact]
    public void A_calibrated_row_is_appended_next_to_the_pre_calibration_one()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.PreCalibrationStatus));
            LedgerWriter.Append(path, Entry(LedgerWriter.CalibratedStatusFor(Fingerprint)));

            var rows = LedgerWriter.ReadRows(path);

            Assert.Equal(2, rows.Count);
            Assert.Contains(LedgerWriter.PreCalibrationStatus, rows[0]);
            Assert.Contains(LedgerWriter.CalibratedStatus, rows[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void The_header_explains_the_calibrated_status_and_the_fingerprint()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.PreCalibrationStatus));

            var text = File.ReadAllText(path);

            Assert.Contains("calibré", text);
            Assert.Contains("empreinte", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static LedgerEntry Entry(string status) => new(
        new DateOnly(2026, 8, 5), "20260728-v5-gemma-d79a63b9", "eval/boards.dev.jsonl",
        "fr/board-clues-per-direction.md", 5, "gemma-4-12b-qat", "2026-07-28",
        "temp 1.0 / topP 0.95 / maxRetries 0",
        new MetricsReport(
            "20260728-v5-gemma-d79a63b9", "eval/boards.dev.jsonl", "416b819a41a1", 40, 160,
            0.94, 0.81, 0.02, 0.363, 0.41, 0.656, 0.55, 0.12, [], 0.01, 160, 160,
            new Dictionary<(string, string), double>()),
        status, "baseline v5", "neutre", "thinking OFF, ctx 16k");
}
