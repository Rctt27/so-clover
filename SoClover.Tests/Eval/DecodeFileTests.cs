using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class DecodeFileTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"run-{Guid.NewGuid():N}.decoded.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static DecodeManifest Manifest() =>
        new("manifest", "decode-1", new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc),
            "20260727-v5-gemma-a1b2c3d4", "eval/boards.dev.jsonl", "a1b2c3d4e5f6",
            "OpenAI", "http://localhost:1234/v1", "qwen3-8b", "2026-07-27", "deadbeef",
            0.3, null, 512,
            "Decoder/Prompts/fr/decode-clue.md", 1,
            "Decoder/Prompts/fr/decode-board.md", 1,
            3, RunFile.HarnessVersion, "décodeur chargé, thinking OFF");

    private static ClueDecodeLine Decode(string boardId, string direction, int index, double? r = 0.5) =>
        new("decode", boardId, direction, index, r is null ? null : ["Chirurgien", "Infirmière"],
            r, "123456", r is null ? "outOfVocabulary" : null, 3400);

    private static BoardDecodeLine BoardDecode(string boardId) =>
        new("boardDecode", boardId,
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Top"] = ["Chirurgien", "Enfant"],
                ["Right"] = ["Île", "Forêt"],
                ["Bottom"] = ["Vague", "Miel"],
                ["Left"] = ["Tambour", "Ciel"],
            },
            0.75, false, "654321", null, 5200);

    [Fact]
    public void Round_trips_both_line_kinds()
    {
        DecodeFile.WriteManifest(_path, Manifest());
        DecodeFile.AppendClueDecode(_path, Decode("dev-007", "Top", 0));
        DecodeFile.AppendBoardDecode(_path, BoardDecode("dev-007"));
        DecodeFile.AppendClueDecode(_path, Decode("dev-007", "Top", 1, r: null));

        var decoded = DecodeFile.Read(_path);

        Assert.Equal("decode-1", decoded.Manifest.DecodeRunId);
        Assert.Equal(3, decoded.Manifest.DecodesPerClue);
        Assert.Equal(2, decoded.ClueDecodes.Count);
        Assert.Single(decoded.BoardDecodes);
        Assert.Equal(0.75, decoded.BoardDecodes[0].BoardPositions);
        Assert.Equal(4, decoded.BoardDecodes[0].Assignment!.Count);
        Assert.Equal("outOfVocabulary", decoded.ClueDecodes[1].DecodeFailureKind);
    }

    [Fact]
    public void Read_dispatches_on_the_kind_discriminator()
    {
        DecodeFile.WriteManifest(_path, Manifest());
        DecodeFile.AppendBoardDecode(_path, BoardDecode("dev-001"));
        DecodeFile.AppendClueDecode(_path, Decode("dev-001", "Left", 2));

        var decoded = DecodeFile.Read(_path);

        Assert.Single(decoded.BoardDecodes);
        Assert.Single(decoded.ClueDecodes);
        Assert.Equal("Left", decoded.ClueDecodes[0].Direction);
    }

    [Fact]
    public void Read_tolerates_a_truncated_last_line()
    {
        DecodeFile.WriteManifest(_path, Manifest());
        DecodeFile.AppendClueDecode(_path, Decode("dev-001", "Top", 0));
        File.AppendAllText(_path, "{\"kind\":\"decode\",\"boardId\":\"dev-00");

        var decoded = DecodeFile.Read(_path);

        Assert.Single(decoded.ClueDecodes);
    }

    [Fact]
    public void Read_refuses_an_incompatible_harness_version()
    {
        DecodeFile.WriteManifest(_path, Manifest() with { HarnessVersion = 99 });

        Assert.Throws<RunIntegrityException>(() => DecodeFile.Read(_path));
    }

    [Fact]
    public void Read_refuses_a_first_line_that_is_not_a_manifest()
    {
        File.WriteAllText(_path, EvalJson.Serialize(Decode("dev-001", "Top", 0)) + "\n");

        Assert.Throws<RunIntegrityException>(() => DecodeFile.Read(_path));
    }

    [Fact]
    public void Lines_are_appended_never_rewritten()
    {
        DecodeFile.WriteManifest(_path, Manifest());
        DecodeFile.AppendClueDecode(_path, Decode("dev-001", "Top", 0));
        var afterFirst = File.ReadAllText(_path);

        DecodeFile.AppendClueDecode(_path, Decode("dev-001", "Top", 1));

        Assert.StartsWith(afterFirst, File.ReadAllText(_path));
    }

    [Fact]
    public void PathFor_derives_the_decoded_path_from_the_run_path()
    {
        Assert.Equal(
            Path.Combine("eval", "runs", "20260727-v5-gemma-a1b2c3d4.decoded.jsonl"),
            DecodeFile.PathFor(Path.Combine("eval", "runs", "20260727-v5-gemma-a1b2c3d4.jsonl")));
    }

    [Fact]
    public void Read_of_a_missing_file_yields_null_rather_than_throwing()
    {
        Assert.Null(DecodeFile.ReadOrNull(_path));
    }
}
