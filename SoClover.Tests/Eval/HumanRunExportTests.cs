using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class HumanRunExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"human-run-{Guid.NewGuid():N}");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private ElicitationContents Elicitation() =>
        HumanTestData.Elicitation(
            _bench, ElicitationPlan.Build(_bench, seed: 42, targetCount: 40), passEvery: 8);

    [Fact]
    public void Le_pseudo_run_est_relu_par_RunFile()
    {
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, Elicitation(), "eval/boards.dev.jsonl",
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

        var path = HumanRunExport.Write(_directory, manifest, attempts);
        var run = RunFile.Read(path);

        Assert.StartsWith("human-20260801-", run.Manifest.RunId, StringComparison.Ordinal);
        Assert.Equal(HumanRunExport.ModelId, run.Manifest.ModelId);
        Assert.Equal(HumanRunExport.GenerationModeName, run.Manifest.GenerationMode);
        Assert.Null(run.Manifest.PromptVersion);
        Assert.Equal(_bench.Manifest.BenchHash, run.Manifest.BenchHash);
        Assert.Equal(40, run.Attempts.Count);
    }

    [Fact]
    public void Un_pass_devient_une_tentative_invalide_qui_reste_au_denominateur()
    {
        var elicitation = Elicitation();
        var (manifest, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);

        var passKeys = elicitation.Elicitations
            .Where(e => e.Outcome == Outcomes.Pass)
            .Select(e => (e.BoardId, e.Direction))
            .ToHashSet();

        Assert.NotEmpty(passKeys);
        foreach (var attempt in attempts.Where(a => passKeys.Contains((a.BoardId, a.Direction))))
        {
            Assert.False(attempt.Valid);
            Assert.Null(attempt.Clue);
            Assert.Equal(HumanRunExport.PassFailureKind, attempt.FailureKind);
        }

        // A-1 : la direction reste dans le run, elle n'en est jamais retirée.
        Assert.Equal(elicitation.Elicitations.Count, attempts.Count);
        Assert.Equal(0, manifest.MaxRetries);
    }

    [Fact]
    public void La_latence_reprend_le_chrono_humain_en_millisecondes()
    {
        var elicitation = Elicitation();
        var (_, attempts) = HumanRunExport.Build(
            _bench, elicitation, "eval/boards.dev.jsonl", DateTime.UtcNow);

        var first = elicitation.Elicitations[0];
        var exported = attempts.Single(a => a.BoardId == first.BoardId && a.Direction == first.Direction);

        Assert.Equal(first.ElapsedSeconds * 1000L, exported.LatencyMs);
    }
}
