using SoClover.Eval.Cli;
using Xunit;

namespace SoClover.Tests.Eval;

public class ResumeProgressTests
{
    [Fact]
    public void A_fresh_run_counts_from_one_over_the_whole_run()
    {
        var progress = ResumeProgress.From(total: 160, pending: 160);

        Assert.Equal("[1/160]", progress.Counter(doneThisSession: 1));
    }

    [Fact]
    public void A_resumed_run_counts_after_the_units_already_written_over_the_whole_run()
    {
        // Retour opérateur du 2026-09-24 : une reprise après 6 directions affichait [1/154],
        // ce qui laisse croire que le run ne compte que 154 directions.
        var progress = ResumeProgress.From(total: 160, pending: 154);

        Assert.Equal(6, progress.AlreadyDone);
        Assert.Equal("[7/160]", progress.Counter(doneThisSession: 1));
        Assert.Equal("[160/160]", progress.Counter(doneThisSession: 154));
    }

    [Fact]
    public void The_estimate_extrapolates_this_session_rate_to_the_units_left_in_this_session()
    {
        var progress = ResumeProgress.From(total: 160, pending: 154);

        // 4 unités en 20 s cette session → 5 s/unité ; il en reste 150.
        var eta = progress.Remaining(doneThisSession: 4, elapsed: TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(750), eta);
    }

    [Fact]
    public void The_estimate_is_zero_before_the_first_unit_completes()
    {
        var progress = ResumeProgress.From(total: 160, pending: 154);

        Assert.Equal(TimeSpan.Zero, progress.Remaining(doneThisSession: 0, elapsed: TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void More_pending_units_than_the_total_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResumeProgress.From(total: 10, pending: 11));
    }
}
