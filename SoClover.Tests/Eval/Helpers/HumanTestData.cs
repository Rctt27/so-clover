using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Tests.Eval.Helpers;

/// <summary>
/// Fabriques partagées des suites P4-P5. Les bancs produits ici sont SYNTHÉTIQUES : mots
/// numérotés, sans radical commun, pour qu'aucun test ne dépende du dictionnaire réel — même
/// motif que DeterministicWordDictionary côté jeu.
/// </summary>
public static class HumanTestData
{
    public static BenchContents Bench(int boardCount = 10, string benchHash = "aaaaaaaaaaaa")
    {
        var boards = new List<BenchBoard>(boardCount);
        for (var b = 0; b < boardCount; b++)
        {
            var cards = new List<IReadOnlyList<string>>(4);
            for (var c = 0; c < 4; c++)
                cards.Add([$"mot{b}x{c}x0", $"mot{b}x{c}x1", $"mot{b}x{c}x2", $"mot{b}x{c}x3"]);

            var directions = BoardGeometry.AllDirections
                .Select(d => new BenchDirection(
                    d.ToString(),
                    BenchBoardMapperProbe.ReferenceWords(cards, d)))
                .ToList();

            boards.Add(new BenchBoard(
                Kind: "board",
                BoardId: $"dev-{b:D3}",
                Cards: cards.AsReadOnly(),
                Directions: directions.AsReadOnly(),
                Strata: new BenchStrata(null)));
        }

        var manifest = new BenchManifest(
            Kind: "manifest",
            BenchId: "dev",
            Seed: 1,
            BoardCount: boardCount,
            Language: "Français_OFF",
            DictionaryFile: "Français_OFF.txt",
            DictionaryHash: "bbbbbbbbbbbb",
            PrngAlgorithm: Xoshiro256SS.AlgorithmName,
            GeneratorVersion: 1,
            CreatedAtUtc: new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc),
            BenchHash: benchHash);

        return new BenchContents(manifest, boards.AsReadOnly());
    }

    public static ElicitationManifest ElicitationManifest(
        string benchHash, int targetCount = 40, int quotaBeforePause = 25,
        string? candidatesRunId = null) => new(
        Kind: "manifest",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: benchHash,
        Seed: 42,
        TargetCount: targetCount,
        TimerSeconds: 90,
        QuotaBeforePause: quotaBeforePause,
        CandidatesRunId: candidatesRunId,
        HarnessVersion: HumanFile.HarnessVersion,
        CreatedAtUtc: new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc));

    public static ComparisonManifest ComparisonManifest(
        string benchHash, int targetCount, int quotaBeforePause = 50,
        double hoursSinceElicitation = 26.4, bool earlyStart = false) => new(
        Kind: "manifest",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: benchHash,
        Seed: 20260730001,
        ElicitationFile: "eval/human/elicitation.dev.jsonl",
        Runs:
        [
            new ComparisonRunRef("run-a", "eval/runs/run-a.jsonl"),
            new ComparisonRunRef("run-b", "eval/runs/run-b.jsonl"),
        ],
        TargetCount: targetCount,
        QuotaBeforePause: quotaBeforePause,
        HoursSinceElicitation: hoursSinceElicitation,
        EarlyStart: earlyStart,
        HarnessVersion: HumanFile.HarnessVersion,
        CreatedAtUtc: new DateTime(2026, 7, 31, 8, 0, 0, DateTimeKind.Utc));

    /// <summary>Run synthétique : un indice valide par direction, préfixé pour être traçable.</summary>
    public static RunContents Run(BenchContents bench, string runId, string cluePrefix)
    {
        var manifest = new RunManifest(
            Kind: "manifest", RunId: runId, Stage: "generate",
            CreatedAtUtc: new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            BenchFile: "eval/boards.dev.jsonl", BenchHash: bench.Manifest.BenchHash,
            PromptFile: null, PromptVersion: null, GenerationMode: "PerDirection",
            ReasoningEnabled: false, Provider: "None", BaseUrl: string.Empty,
            ModelId: cluePrefix, ModelSnapshotDate: null, ProviderModelListHash: null,
            Temperature: 1.0, TopP: null, MaxOutputTokens: null, MaxRetries: 0,
            Language: bench.Manifest.Language, HarnessVersion: RunFile.HarnessVersion,
            OperatorNotes: null);

        var attempts = new List<RunAttempt>();
        foreach (var board in bench.Boards)
        {
            foreach (var direction in BoardGeometry.AllDirections)
            {
                attempts.Add(new RunAttempt(
                    Kind: "attempt", BoardId: board.BoardId, Direction: direction.ToString(),
                    Attempt: 0, Clue: $"{cluePrefix}-{board.BoardId}-{direction}",
                    Candidates: [$"{cluePrefix}-c1 (fort, fort)", $"{cluePrefix}-c2 (moyen, fort)"],
                    Explanation: "synthétique", Valid: true, RejectionRules: [], FailureKind: null,
                    LatencyMs: 1000, InputTokens: null, OutputTokens: null,
                    PromptVersion: null, EffectiveModel: cluePrefix));
            }
        }

        return new RunContents(manifest, attempts.AsReadOnly());
    }

    /// <summary>
    /// Corpus d'élicitation synthétique en mémoire (aucun fichier). <paramref name="passEvery"/>
    /// insère un <c>pass</c> tous les N items, <paramref name="assistCount"/> ajoute des lignes
    /// <c>assist</c> sur les premiers items.
    /// </summary>
    public static ElicitationContents Elicitation(
        BenchContents bench,
        IReadOnlyList<PlanItem> plan,
        int assistCount = 0,
        int passEvery = 0,
        DateTime? authoredAtUtc = null)
    {
        var when = authoredAtUtc ?? new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc);
        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        var lines = new List<ElicitationLine>();
        var assists = new List<AssistLine>();

        for (var i = 0; i < plan.Count; i++)
        {
            var item = plan[i];
            var isPass = passEvery > 0 && i % passEvery == passEvery - 1;
            var direction = Enum.Parse<Direction>(item.Direction);

            lines.Add(new ElicitationLine(
                Kind: "elicitation",
                BoardId: item.BoardId,
                Direction: item.Direction,
                ReferenceWords: BenchBoardMapper.ReferenceWords(boards[item.BoardId], direction),
                Outcome: isPass ? Outcomes.Pass : (i % 2 == 0 ? Outcomes.Solide : Outcomes.Tiede),
                Clue: isPass ? null : $"humain-{item.BoardId}-{item.Direction}",
                ElapsedSeconds: 30 + i,
                ServerElapsedSeconds: 32 + i,
                RelationType: isPass ? null : RelationTypes.All[i % RelationTypes.All.Count],
                RejectedAttempts: [],
                SessionId: "s-fixture",
                ItemOrdinal: i + 1,
                AuthoredAtUtc: when.AddMinutes(i)));

            if (i < assistCount && !isPass)
                assists.Add(new AssistLine(
                    Kind: "assist", BoardId: item.BoardId, Direction: item.Direction,
                    AssistedClue: $"assiste-{item.BoardId}-{item.Direction}",
                    Notes: null, AuthoredAtUtc: when.AddMinutes(i).AddSeconds(30)));
        }

        return new ElicitationContents(
            ElicitationManifest(bench.Manifest.BenchHash, plan.Count),
            lines.AsReadOnly(),
            assists.AsReadOnly());
    }

    /// <summary>
    /// Dérive la paire de référence par la même convention que le générateur de bancs.
    /// BenchBoardMapper.DeriveReferenceWords est `internal` au projet Eval ; le projet de test y
    /// a accès par InternalsVisibleTo, mais on passe par ce point unique pour que le jour où la
    /// convention bouge, un seul endroit des tests suive.
    /// </summary>
    private static class BenchBoardMapperProbe
    {
        public static IReadOnlyList<string> ReferenceWords(
            IReadOnlyList<IReadOnlyList<string>> cards, Direction edge)
        {
            var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(edge);
            return [cards[(int)cardA][(int)faceA], cards[(int)cardB][(int)faceB]];
        }
    }
}
