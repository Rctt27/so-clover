using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Prompts;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;

namespace SoClover.Tests.Eval.Helpers;

internal static class LangfuseFixtures
{
    public static MetricsReport Metrics(MetricCounts? counts = null) => new(
        RunId: "20260728-v5-google-gemma-4-12b-qat-d79a63b9",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        BoardCount: 40,
        DirectionCount: 160,
        ValidRate: 0.963,
        FirstAttemptRate: 0.963,
        ParseFailureRate: 0.031,
        Recovery: 0.370,
        Strict2Of2: 0.026,
        HalfRate: 0.630,
        BoardPositions: 0.278,
        BoardSolvedFirstTry: 0.0,
        ConfusionTop: [],
        DecodeFailureRate: 0.038,
        ItemsCompleted: 160,
        ItemsExpected: 160,
        PerItemRBar: new Dictionary<(string BoardId, string Direction), double>(),
        Counts: counts ?? (MetricCounts.Zero with { DecodedItems = 154, ScoredBoards = 40, Decodes = 480 }));

    public static readonly PromptProvenance LangfuseClue =
        new("langfuse", "decoder-fr-clue", 3, "production", new string('a', 64));

    // Adaptation au code réel (brief §Interfaces) : `BenchBoardMapper.ReferenceWords(board, d)`
    // (publique) LIT l'oracle gelé dans `board.Directions`, qui est encore `[]` à ce point de la
    // construction — elle lèverait. `DeriveReferenceWords(cards, edge)` (interne, visible ici via
    // InternalsVisibleTo) CALCULE la paire depuis la géométrie, sans dépendre de `Directions` :
    // c'est la même surcharge que `BenchGenerator` utilise pour produire l'oracle au départ.
    public static BenchContents Bench(string benchId = "dev")
    {
        var board = new BenchBoard(
            Kind: "board",
            BoardId: "dev-001",
            Cards:
            [
                ["Paradis", "Membre", "Vêtement", "Chêne"],
                ["Terrasse", "Tarte", "Déchet", "Voleur"],
                ["Maître", "Herbe", "Liquide", "Fable"],
                ["Miroir", "Fuite", "Collier", "Ampoule"],
            ],
            Directions: [],
            Strata: new BenchStrata(null));

        board = board with
        {
            Directions = BoardGeometry.AllDirections
                .Select(d => new BenchDirection(d.ToString(), BenchBoardMapper.DeriveReferenceWords(board.Cards, d)))
                .ToList(),
        };

        return new BenchContents(
            new BenchManifest(
                Kind: "manifest", BenchId: benchId, Seed: 20260726001, BoardCount: 1, Language: "Français_OFF",
                DictionaryFile: "Français_OFF.txt", DictionaryHash: "78240a42a83c", PrngAlgorithm: "xoshiro256ss",
                GeneratorVersion: 1, CreatedAtUtc: new DateTime(2026, 7, 27, 23, 15, 16, DateTimeKind.Utc),
                BenchHash: "416b819a41a1"),
            [board]);
    }

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

    /// <summary>
    /// Run sur <see cref="Bench"/> : Top réussit à la 2e tentative, Right et Bottom à la 1re,
    /// Left échoue au format. Les latences sont choisies pour rendre la chronologie vérifiable.
    /// </summary>
    public static RunContents Run() => new(
        RunManifest(),
        [
            Attempt("Top", 1, null, valid: false, failure: "unparseable", latencyMs: 1000),
            Attempt("Top", 2, "Jardin", valid: true, failure: null, latencyMs: 2000),
            Attempt("Right", 1, "Cuisine", valid: true, failure: null, latencyMs: 1500),
            Attempt("Bottom", 1, "Bijou", valid: true, failure: null, latencyMs: 1500),
            Attempt("Left", 1, null, valid: false, failure: "empty", latencyMs: 500),
        ]);

    /// <summary>Trois décodages par direction ; Left n'en a aucun (D6), Bottom en a un en échec de format.</summary>
    public static DecodeContents Decoded() => new(
        DecodeManifest(),
        [
            Decode("Top", 0, ["Paradis", "Terrasse"], 1.0),
            Decode("Top", 1, ["Paradis", "Herbe"], 0.5),
            Decode("Top", 2, ["Membre", "Herbe"], 0.0),
            Decode("Right", 0, ["Tarte", "Herbe"], 1.0),
            Decode("Right", 1, ["Tarte", "Herbe"], 1.0),
            Decode("Right", 2, ["Tarte", "Fable"], 0.5),
            Decode("Bottom", 0, ["Liquide", "Collier"], 1.0),
            Decode("Bottom", 1, null, null),
            Decode("Bottom", 2, ["Collier", "Miroir"], 0.5),
        ],
        []);

    private static RunAttempt Attempt(string direction, int attempt, string? clue, bool valid, string? failure, long latencyMs) => new(
        Kind: "attempt", BoardId: "dev-001", Direction: direction, Attempt: attempt, Clue: clue,
        Candidates: null, Explanation: clue is null ? null : "explication", Valid: valid,
        RejectionRules: [], FailureKind: failure, LatencyMs: latencyMs, InputTokens: 1200, OutputTokens: 80,
        PromptVersion: 5, EffectiveModel: "google/gemma-4-12b-qat");

    private static ClueDecodeLine Decode(string direction, int index, IReadOnlyList<string>? picked, double? r) => new(
        Kind: "clueDecode", BoardId: "dev-001", Direction: direction, DecodeIndex: index, Picked: picked, R: r,
        ShuffleSeed: "seed", DecodeFailureKind: picked is null ? "unparseable" : null, LatencyMs: 800);
}
