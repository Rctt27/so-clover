using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using Xunit;

namespace SoClover.Tests.Eval;

public class RunFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"run-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static RunManifest Manifest(
        string runId = "20260727-v5-gemma-a1b2c3d4",
        string? notes = "thinking OFF, ctx 16k",
        DateTime? createdAt = null) =>
        new(
            Kind: "manifest",
            RunId: runId,
            Stage: "generate",
            CreatedAtUtc: createdAt ?? new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc),
            BenchFile: "eval/boards.dev.jsonl",
            BenchHash: "a1b2c3d4e5f6",
            PromptFile: "/app/Infrastructure/AI/Prompts/fr/board-clues-per-direction.md",
            PromptVersion: 5,
            GenerationMode: "PerDirection",
            ReasoningEnabled: false,
            Provider: "OpenAI",
            BaseUrl: "http://localhost:1234/v1",
            ModelId: "gemma-4-12b-qat",
            ModelSnapshotDate: "2026-07-27",
            ProviderModelListHash: "deadbeef",
            Temperature: 1.0,
            TopP: 0.95,
            MaxOutputTokens: 4096,
            MaxRetries: 0,
            Language: "Français_OFF",
            HarnessVersion: RunFile.HarnessVersion,
            OperatorNotes: notes);

    private static RunAttempt Attempt(
        string boardId = "dev-007", string direction = "Top", int attempt = 0,
        string? clue = "Hôpital", bool valid = true, string? failureKind = null) =>
        new(
            Kind: "attempt",
            BoardId: boardId,
            Direction: direction,
            Attempt: attempt,
            Clue: clue,
            Candidates: ["Hôpital (fort, fort)", "Pédiatre (fort, fort)"],
            Explanation: "lien médical",
            Valid: valid,
            RejectionRules: valid ? [] : ["ExactMatch"],
            FailureKind: failureKind,
            LatencyMs: 8123,
            InputTokens: 2100,
            OutputTokens: 180,
            PromptVersion: 5,
            EffectiveModel: "gemma-4-12b-qat");

    [Fact]
    public void Round_trips_manifest_and_attempts()
    {
        RunFile.WriteManifest(_path, Manifest());
        RunFile.AppendAttempt(_path, Attempt());
        RunFile.AppendAttempt(_path, Attempt(direction: "Right", valid: false));

        var run = RunFile.Read(_path);

        Assert.Equal("20260727-v5-gemma-a1b2c3d4", run.Manifest.RunId);
        Assert.Equal(2, run.Attempts.Count);
        Assert.Equal("Hôpital", run.Attempts[0].Clue);
        Assert.Equal(["ExactMatch"], run.Attempts[1].RejectionRules);
        Assert.Equal(["Hôpital (fort, fort)", "Pédiatre (fort, fort)"], run.Attempts[0].Candidates);
    }

    // La reprise doit survivre à une interruption en plein milieu d'une écriture de ligne.
    [Fact]
    public void Read_tolerates_a_truncated_last_line()
    {
        RunFile.WriteManifest(_path, Manifest());
        RunFile.AppendAttempt(_path, Attempt());
        File.AppendAllText(_path, "{\"kind\":\"attempt\",\"boardId\":\"dev-008\",\"direct");

        var run = RunFile.Read(_path);

        Assert.Single(run.Attempts);
        Assert.Equal("dev-007", run.Attempts[0].BoardId);
    }

    [Fact]
    public void Read_refuses_an_incompatible_harness_version()
    {
        RunFile.WriteManifest(_path, Manifest() with { HarnessVersion = 99 });

        var ex = Assert.Throws<RunIntegrityException>(() => RunFile.Read(_path));
        Assert.Contains("harnessVersion", ex.Message);
    }

    [Fact]
    public void Read_refuses_a_file_whose_first_line_is_not_a_manifest()
    {
        File.WriteAllText(_path, EvalJson.Serialize(Attempt()) + "\n");

        Assert.Throws<RunIntegrityException>(() => RunFile.Read(_path));
    }

    // Deux runs de configuration identique portent le même hash8 : c'est voulu, il signale
    // un re-run de la même configuration (détection de la dérive du modèle).
    [Fact]
    public void ComputeHash8_ignores_run_id_creation_date_and_operator_notes()
    {
        var a = Manifest(runId: "run-a", notes: "thinking OFF", createdAt: new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));
        var b = Manifest(runId: "run-b", notes: "thinking ON", createdAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(RunFile.ComputeHash8(a), RunFile.ComputeHash8(b));
    }

    [Fact]
    public void ComputeHash8_changes_when_a_meaningful_setting_changes()
    {
        var baseline = Manifest();

        Assert.NotEqual(RunFile.ComputeHash8(baseline), RunFile.ComputeHash8(baseline with { Temperature = 0.7 }));
        Assert.NotEqual(RunFile.ComputeHash8(baseline), RunFile.ComputeHash8(baseline with { PromptVersion = 6 }));
        Assert.NotEqual(RunFile.ComputeHash8(baseline), RunFile.ComputeHash8(baseline with { ModelId = "autre" }));
        Assert.NotEqual(RunFile.ComputeHash8(baseline), RunFile.ComputeHash8(baseline with { ReasoningEnabled = true }));
    }

    [Fact]
    public void ComputeHash8_is_eight_hex_characters()
    {
        Assert.Matches("^[0-9a-f]{8}$", RunFile.ComputeHash8(Manifest()));
    }

    [Fact]
    public void BuildRunId_follows_the_documented_shape()
    {
        var runId = RunFile.BuildRunId(
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc), 5, "Gemma-4-12B_QAT", "a1b2c3d4");

        Assert.Equal("20260727-v5-gemma-4-12b-qat-a1b2c3d4", runId);
    }

    [Fact]
    public void BuildRunId_marks_an_unknown_prompt_version()
    {
        var runId = RunFile.BuildRunId(
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc), null, "random-baseline", "00000000");

        Assert.Equal("20260727-vnone-random-baseline-00000000", runId);
    }

    // Une direction est terminée si elle a une tentative valide, ou maxAttempts tentatives.
    [Fact]
    public void CompletedDirections_counts_a_valid_attempt_as_terminal()
    {
        RunFile.WriteManifest(_path, Manifest());
        RunFile.AppendAttempt(_path, Attempt(direction: "Top", attempt: 0, valid: true));

        var completed = RunFile.CompletedDirections(RunFile.Read(_path), maxAttempts: 3);

        Assert.Contains(("dev-007", "Top"), completed);
    }

    [Fact]
    public void CompletedDirections_counts_an_exhausted_retry_budget_as_terminal()
    {
        RunFile.WriteManifest(_path, Manifest());
        for (var i = 0; i < 3; i++)
            RunFile.AppendAttempt(_path, Attempt(direction: "Right", attempt: i, valid: false));

        var completed = RunFile.CompletedDirections(RunFile.Read(_path), maxAttempts: 3);

        Assert.Contains(("dev-007", "Right"), completed);
    }

    [Fact]
    public void CompletedDirections_leaves_a_partially_attempted_direction_open()
    {
        RunFile.WriteManifest(_path, Manifest());
        RunFile.AppendAttempt(_path, Attempt(direction: "Bottom", attempt: 0, valid: false));

        var completed = RunFile.CompletedDirections(RunFile.Read(_path), maxAttempts: 3);

        Assert.DoesNotContain(("dev-007", "Bottom"), completed);
    }

    [Fact]
    public void Attempts_are_appended_never_rewritten()
    {
        RunFile.WriteManifest(_path, Manifest());
        RunFile.AppendAttempt(_path, Attempt(direction: "Top"));
        var afterFirst = File.ReadAllText(_path);

        RunFile.AppendAttempt(_path, Attempt(direction: "Right"));
        var afterSecond = File.ReadAllText(_path);

        Assert.StartsWith(afterFirst, afterSecond);
    }
}
