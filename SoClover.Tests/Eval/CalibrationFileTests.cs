using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationFileTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"calibration-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static CalibrationManifest Manifest(
        int harnessVersion = CalibrationFile.HarnessVersion,
        string benchHash = "416b819a41a1",
        string fingerprint = "3f2a91c4e0d1",
        int decodesPerClue = 5,
        double epsilon = 0.0) => new(
        Kind: "manifest",
        CalibrationId: $"20260805-{fingerprint}",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.dev.jsonl",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: benchHash,
        CoupleCount: 100,
        ClueCount: 187,
        DecoderFingerprint: fingerprint,
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "qwen/qwen3-8b",
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "0123456789ab",
        Temperature: 0.3,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "C:/build/Decoder/Prompts/fr/decode-clue.md",
        CluePromptVersion: 2,
        DecodesPerClue: decodesPerClue,
        Epsilon: epsilon,
        HarnessVersion: harnessVersion,
        OperatorNotes: "qwen3-8b thinking OFF, ctx 8k");

    private static CalibrationDecode Decode(
        string boardId = "dev-007", string direction = "Top", string clue = "Pédiatre",
        int decodeIndex = 0, double? r = 1.0, string? failureKind = null) => new(
        Kind: "calibrationDecode",
        BoardId: boardId,
        Direction: direction,
        Clue: clue,
        DecodeIndex: decodeIndex,
        Picked: failureKind is null ? ["Chirurgien", "Enfant"] : null,
        R: failureKind is null ? r : null,
        ShuffleSeed: "-123456789",
        DecodeFailureKind: failureKind,
        LatencyMs: 2900);

    [Fact]
    public void Round_trips_the_manifest_and_the_decodes()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode());
        CalibrationFile.AppendDecode(_path, Decode(decodeIndex: 1, r: 0.5));

        var contents = CalibrationFile.Read(_path);

        Assert.Equal("20260805-3f2a91c4e0d1", contents.Manifest.CalibrationId);
        Assert.Equal(5, contents.Manifest.DecodesPerClue);
        Assert.Equal(0.0, contents.Manifest.Epsilon);
        Assert.Equal(2, contents.Decodes.Count);
        Assert.Equal("Pédiatre", contents.Decodes[0].Clue);
        Assert.Equal(0.5, contents.Decodes[1].R);
    }

    // LE point du type dédié : en calibration une direction porte DEUX à TROIS indices
    // concurrents. Sous la clé (boardId, direction, decodeIndex) de ClueDecodeLine, ils
    // collisionneraient silencieusement à la reprise.
    [Fact]
    public void Two_distinct_clues_of_the_same_direction_do_not_collide()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode(clue: "Pédiatre", decodeIndex: 0));
        CalibrationFile.AppendDecode(_path, Decode(clue: "Hôpital", decodeIndex: 0));

        var contents = CalibrationFile.Read(_path);
        var keys = contents.Decodes
            .Select(d => (d.BoardId, d.Direction, d.Clue, d.DecodeIndex))
            .Distinct()
            .ToList();

        Assert.Equal(2, contents.Decodes.Count);
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void A_failed_decode_carries_no_picked_and_no_r()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode(failureKind: "unparseable"));

        var decode = CalibrationFile.Read(_path).Decodes.Single();

        Assert.Equal("unparseable", decode.DecodeFailureKind);
        Assert.Null(decode.R);
        Assert.Null(decode.Picked);
    }

    // Même religion que RunFile et DecodeFile : une séance interrompue en pleine écriture
    // ne doit pas rendre le fichier illisible — la reprise regénérera la ligne.
    [Fact]
    public void Tolerates_a_last_line_truncated_mid_write()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode());
        File.AppendAllText(_path, "{\"kind\":\"calibrationDecode\",\"boardId\":\"dev-0");

        var contents = CalibrationFile.Read(_path);

        Assert.Single(contents.Decodes);
    }

    [Fact]
    public void Refuses_an_incompatible_harness_version()
    {
        CalibrationFile.WriteManifest(_path, Manifest(harnessVersion: 99));

        var ex = Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
        Assert.Contains("harnessVersion", ex.Message);
    }

    [Fact]
    public void Refuses_a_file_whose_first_line_is_not_a_manifest()
    {
        File.WriteAllText(_path, EvalJson.Serialize(Decode()) + "\n");

        Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
    }

    [Fact]
    public void Refuses_an_unknown_kind()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        File.AppendAllText(_path, "{\"kind\":\"decode\",\"boardId\":\"dev-007\"}\n");
        File.AppendAllText(_path, EvalJson.Serialize(Decode()) + "\n");

        var ex = Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
        Assert.Contains("kind", ex.Message);
    }

    [Fact]
    public void ReadOrNull_returns_null_when_the_file_is_absent()
    {
        Assert.Null(CalibrationFile.ReadOrNull(_path));
    }

    [Fact]
    public void PathFor_names_the_file_after_the_calibration_id()
    {
        var path = CalibrationFile.PathFor("eval/human", "20260805-3f2a91c4e0d1");

        Assert.Equal("calibration.20260805-3f2a91c4e0d1.jsonl", Path.GetFileName(path));
    }

    [Fact]
    public void ReportPathFor_swaps_the_jsonl_extension_for_json()
    {
        var report = CalibrationFile.ReportPathFor(
            CalibrationFile.PathFor("eval/human", "20260805-3f2a91c4e0d1"));

        Assert.Equal("calibration.20260805-3f2a91c4e0d1.json", Path.GetFileName(report));
    }

    [Fact]
    public void From_projects_a_clue_decode_line_and_adds_the_clue()
    {
        var line = new ClueDecodeLine(
            Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: 2,
            Picked: ["Chirurgien", "Enfant"], R: 1.0, ShuffleSeed: "42",
            DecodeFailureKind: null, LatencyMs: 1234);

        var decode = CalibrationDecode.From(line, "Pédiatre");

        Assert.Equal("calibrationDecode", decode.Kind);
        Assert.Equal("Pédiatre", decode.Clue);
        Assert.Equal("dev-007", decode.BoardId);
        Assert.Equal("Top", decode.Direction);
        Assert.Equal(2, decode.DecodeIndex);
        Assert.Equal(1.0, decode.R);
        Assert.Equal("42", decode.ShuffleSeed);
        Assert.Equal(1234, decode.LatencyMs);
    }
}
