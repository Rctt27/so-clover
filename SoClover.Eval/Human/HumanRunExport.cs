using System.Globalization;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Human;

/// <summary>
/// Projection des indices humains en <b>pseudo-run</b> au format <see cref="RunFile"/>. Les verbes
/// existants s'appliquent alors sans modification : <c>decode</c> décode les indices humains et
/// saute N3 tout seul (aucun board du banc n'a ses 4 directions annotées, comportement déjà en
/// place), puis <c>score</c> et <c>compare</c> font le reste.
/// </summary>
public static class HumanRunExport
{
    public const string ModelId = "human";
    public const string GenerationModeName = "Human";

    /// <summary>
    /// Un <c>pass</c> devient une tentative invalide, jamais une absence : il <b>reste au
    /// dénominateur</b>, conformément à A-1. Retirer les cas durs, c'est retirer la résolution de
    /// l'instrument.
    /// </summary>
    public const string PassFailureKind = "pass";

    public static (RunManifest Manifest, IReadOnlyList<RunAttempt> Attempts) Build(
        BenchContents bench,
        ElicitationContents elicitation,
        string benchFilePath,
        DateTime createdAtUtc)
    {
        var draft = new RunManifest(
            Kind: "manifest",
            RunId: string.Empty,
            Stage: "generate",
            CreatedAtUtc: createdAtUtc,
            BenchFile: benchFilePath.Replace('\\', '/'),
            BenchHash: bench.Manifest.BenchHash,
            PromptFile: null,
            PromptVersion: null,
            GenerationMode: GenerationModeName,
            ReasoningEnabled: false,
            Provider: "None",
            BaseUrl: string.Empty,
            ModelId: ModelId,
            ModelSnapshotDate: null,
            ProviderModelListHash: null,
            Temperature: 0,
            TopP: null,
            MaxOutputTokens: null,
            MaxRetries: 0,
            Language: bench.Manifest.Language,
            HarnessVersion: RunFile.HarnessVersion,
            OperatorNotes: $"séance A, {elicitation.Elicitations.Count} direction(s), " +
                           $"seed {elicitation.Manifest.Seed}");

        var hash8 = RunFile.ComputeHash8(draft);
        var runId = $"human-{createdAtUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hash8}";

        var attempts = elicitation.Elicitations
            .Select(line => new RunAttempt(
                Kind: "attempt",
                BoardId: line.BoardId,
                Direction: line.Direction,
                Attempt: 0,
                Clue: line.Outcome == Outcomes.Pass ? null : line.Clue,
                Candidates: null,
                // L'explication du générateur n'a pas d'équivalent humain ; le type de relation
                // est ce qui s'en rapproche le plus et reste utile au dépouillement.
                Explanation: line.RelationType,
                // L'indice a déjà passé ClueAcceptance à la saisie : le revalider ici ne pourrait
                // que diverger de ce que la séance a réellement accepté.
                Valid: line.Outcome != Outcomes.Pass,
                RejectionRules: [],
                FailureKind: line.Outcome == Outcomes.Pass ? PassFailureKind : null,
                LatencyMs: line.ElapsedSeconds * 1000L,
                InputTokens: null,
                OutputTokens: null,
                PromptVersion: null,
                EffectiveModel: ModelId))
            .ToList()
            .AsReadOnly();

        return (draft with { RunId = runId }, attempts);
    }

    public static string Write(
        string outDirectory, RunManifest manifest, IReadOnlyList<RunAttempt> attempts)
    {
        var path = Path.Combine(outDirectory, $"{manifest.RunId}.jsonl");
        RunFile.WriteManifest(path, manifest);
        foreach (var attempt in attempts)
            RunFile.AppendAttempt(path, attempt);
        return path;
    }
}
