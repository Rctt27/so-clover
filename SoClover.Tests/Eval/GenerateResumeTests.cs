using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using Xunit;

namespace SoClover.Tests.Eval;

public class GenerateResumeTests
{
    private static BenchContents Bench(int boards = 2) =>
        BenchGenerator.Generate(
            "dev", 20260726001, boards,
            Enumerable.Range(1, 100).Select(i => $"Mot{i:D3}").ToList(),
            "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

    private static RunAttempt Attempt(string boardId, Direction dir, int attempt, bool valid) =>
        new("attempt", boardId, dir.ToString(), attempt, valid ? "Indice" : null, null, null,
            valid, [], valid ? null : "empty", 10, null, null, 5, "m");

    private static RunContents Run(params RunAttempt[] attempts) =>
        new(new RunManifest("manifest", "r", "generate", DateTime.UtcNow, "b", "h", null, 5,
                "PerDirection", false, "OpenAI", "u", "m", null, null, 1.0, null, null, 0,
                "Français_OFF", RunFile.HarnessVersion, null),
            attempts);

    [Fact]
    public void Without_an_existing_run_every_direction_is_pending()
    {
        var bench = Bench(boards: 2);

        var pending = GenerateCommand.PendingWork(bench, existing: null, maxAttempts: 3);

        Assert.Equal(8, pending.Count);
    }

    [Fact]
    public void Work_is_ordered_board_by_board_then_by_canonical_direction()
    {
        var bench = Bench(boards: 2);

        var pending = GenerateCommand.PendingWork(bench, existing: null, maxAttempts: 3);

        Assert.Equal("dev-001", pending[0].Board.BoardId);
        Assert.Equal(
            BoardGeometry.AllDirections,
            pending.Take(4).Select(p => p.Direction));
        Assert.Equal("dev-002", pending[4].Board.BoardId);
    }

    [Fact]
    public void A_direction_with_a_valid_attempt_is_skipped()
    {
        var bench = Bench(boards: 1);
        var existing = Run(Attempt("dev-001", Direction.Top, 0, valid: true));

        var pending = GenerateCommand.PendingWork(bench, existing, maxAttempts: 3);

        Assert.Equal(3, pending.Count);
        Assert.DoesNotContain(pending, p => p.Direction == Direction.Top);
    }

    [Fact]
    public void A_direction_whose_retry_budget_is_exhausted_is_skipped()
    {
        var bench = Bench(boards: 1);
        var existing = Run(
            Attempt("dev-001", Direction.Right, 0, valid: false),
            Attempt("dev-001", Direction.Right, 1, valid: false),
            Attempt("dev-001", Direction.Right, 2, valid: false));

        var pending = GenerateCommand.PendingWork(bench, existing, maxAttempts: 3);

        Assert.DoesNotContain(pending, p => p.Direction == Direction.Right);
    }

    [Fact]
    public void A_partially_attempted_direction_stays_pending()
    {
        var bench = Bench(boards: 1);
        var existing = Run(Attempt("dev-001", Direction.Bottom, 0, valid: false));

        var pending = GenerateCommand.PendingWork(bench, existing, maxAttempts: 3);

        Assert.Contains(pending, p => p.Direction == Direction.Bottom);
    }

    [Fact]
    public void Nothing_is_pending_when_every_direction_is_terminal()
    {
        var bench = Bench(boards: 1);
        var existing = Run(BoardGeometry.AllDirections
            .Select(d => Attempt("dev-001", d, 0, valid: true))
            .ToArray());

        Assert.Empty(GenerateCommand.PendingWork(bench, existing, maxAttempts: 3));
    }
}
