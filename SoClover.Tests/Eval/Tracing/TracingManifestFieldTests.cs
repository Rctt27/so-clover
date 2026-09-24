using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Tracing;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

public class TracingManifestFieldTests
{
    [Fact]
    public void Tracing_is_outside_hash8()
    {
        var plain = LangfuseFixtures.RunManifest();
        Assert.Equal(
            RunFile.ComputeHash8(plain),
            RunFile.ComputeHash8(plain with { Tracing = TracingManifest.Langfuse("http://127.0.0.1:3000") }));
    }

    [Fact]
    public void Tracing_is_outside_the_decoder_fingerprint()
    {
        var plain = LangfuseFixtures.DecodeManifest();
        Assert.Equal(
            DecoderFingerprint.FromManifest(plain),
            DecoderFingerprint.FromManifest(plain with { Tracing = TracingManifest.Langfuse("http://h") }));
    }

    [Fact]
    public void A_manifest_without_tracing_serializes_exactly_as_before()
    {
        var json = EvalJson.Serialize(LangfuseFixtures.RunManifest());
        Assert.DoesNotContain("tracing", json);
    }

    [Fact]
    public void Tracing_round_trips_in_the_three_manifests()
    {
        var tracing = TracingManifest.Langfuse("http://h");
        var run = LangfuseFixtures.RunManifest() with { Tracing = tracing };
        var decode = LangfuseFixtures.DecodeManifest() with { Tracing = tracing };
        Assert.Equal(tracing, EvalJson.Deserialize<RunManifest>(EvalJson.Serialize(run)).Tracing);
        Assert.Equal(tracing, EvalJson.Deserialize<DecodeManifest>(EvalJson.Serialize(decode)).Tracing);
        Assert.Contains("\"tracing\":{\"target\":\"off\"}",
            EvalJson.Serialize(CalibrationManifestSample() with { Tracing = TracingManifest.Off }));
    }

    // Même manifeste que le round-trip de HistoricalInvariantsTests ; Tracing prend son défaut.
    private static CalibrationManifest CalibrationManifestSample() => new(
        Kind: "manifest",
        CalibrationId: "calibration-20260805",
        CreatedAtUtc: new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.jsonl",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        CoupleAndAnchorCount: 40,
        ClueCount: 120,
        DecoderFingerprint: "9a829dc206d2",
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "qwen/qwen3-8b",
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "45141160fc31",
        Temperature: 0.3,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "eval/prompts/resolved/aaaaaaaaaaaa/fr/decode-clue.md",
        CluePromptVersion: 2,
        DecodesPerClue: 3,
        Epsilon: 0.05,
        HarnessVersion: 1,
        OperatorNotes: null,
        Quantization: "Q4_K_M",
        LoadedContextLength: 16000,
        CluePrompt: LangfuseFixtures.LangfuseClue);
}
