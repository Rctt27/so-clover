using SoClover.Eval.Decoder;
using SoClover.Eval.Prompts;
using SoClover.Eval.Runner;

namespace SoClover.Tests.Eval.Helpers;

internal static class LangfuseFixtures
{
    public static readonly PromptProvenance LangfuseClue =
        new("langfuse", "decoder-fr-clue", 3, "production", new string('a', 64));

    public static RunManifest RunManifest(PromptProvenance? prompt = null) => new(
        Kind: "manifest",
        RunId: "20260728-v5-google-gemma-4-12b-qat-d79a63b9",
        Stage: "generate",
        CreatedAtUtc: new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc),
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        PromptFile: "eval/prompts/resolved/aaaaaaaaaaaa/fr/board-clues-per-direction.md",
        PromptVersion: 5,
        GenerationMode: "PerDirection",
        ReasoningEnabled: false,
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "google/gemma-4-12b-qat",
        ModelSnapshotDate: "2026-07-28",
        ProviderModelListHash: null,
        Temperature: 1.0,
        TopP: 0.95,
        MaxOutputTokens: 4096,
        MaxRetries: 0,
        Language: "Français_OFF",
        HarnessVersion: 1,
        OperatorNotes: "thinking OFF, ctx 16k",
        Prompt: prompt);

    public static DecodeManifest DecodeManifest(PromptProvenance? clue = null) => new(
        Kind: "manifest",
        DecodeRunId: "20260728-v5-google-gemma-4-12b-qat-d79a63b9+decode-20260804090000",
        CreatedAtUtc: new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc),
        GeneratorRunId: "20260728-v5-google-gemma-4-12b-qat-d79a63b9",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "qwen/qwen3-8b",
        ModelSnapshotDate: "2026-08-04",
        ProviderModelListHash: null,
        Temperature: 0.3,
        TopP: 1.0,
        MaxOutputTokens: 512,
        CluePromptFile: "eval/prompts/resolved/aaaaaaaaaaaa/fr/decode-clue.md",
        CluePromptVersion: 4,
        BoardPromptFile: "eval/prompts/resolved/bbbbbbbbbbbb/fr/decode-board.md",
        BoardPromptVersion: 1,
        DecodesPerClue: 3,
        HarnessVersion: 1,
        OperatorNotes: null,
        CluePrompt: clue);
}
