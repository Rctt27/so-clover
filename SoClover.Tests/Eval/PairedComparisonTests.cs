using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class PairedComparisonTests
{
    private static MetricsReport Report(
        IReadOnlyDictionary<(string, string), double> perItem,
        string benchHash = "a1b2c3d4e5f6",
        double validRate = 0.94,
        double boardSolvedFirstTry = 0.12)
    {
        var recovery = perItem.Count == 0 ? 0.0 : perItem.Values.Average();
        return new MetricsReport(
            "run", "eval/boards.dev.jsonl", benchHash, 1, perItem.Count,
            validRate, 0.8, 0.02, recovery, 0.4, 0.3, 0.55, boardSolvedFirstTry,
            [], 0.01, perItem.Count, perItem.Count, perItem);
    }

    private static Dictionary<(string, string), double> Items(params double[] values) =>
        values.Select((v, i) => (Key: ($"dev-{i / 4 + 1:D3}", Direction(i)), Value: v))
              .ToDictionary(x => x.Key, x => x.Value);

    private static string Direction(int i) =>
        new[] { "Top", "Right", "Bottom", "Left" }[i % 4];

    // Erreur de protocole : elle doit être impossible, pas déconseillée.
    [Fact]
    public void Refuses_two_runs_from_different_benches()
    {
        var baseline = Report(Items(0.5, 0.5, 0.5, 0.5), benchHash: "aaaaaaaaaaaa");
        var variant = Report(Items(0.6, 0.6, 0.6, 0.6), benchHash: "bbbbbbbbbbbb");

        var ex = Assert.Throws<MismatchedBenchException>(() => PairedComparison.Compare(baseline, variant));
        Assert.Contains("benchHash", ex.Message);
    }

    [Fact]
    public void Delta_is_the_mean_of_per_item_differences()
    {
        var baseline = Report(Items(0.0, 0.5, 1.0, 0.5));
        var variant = Report(Items(0.5, 0.5, 1.0, 1.0));

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal(4, result.PairedItemCount);
        Assert.Equal(0.25, result.DeltaRecovery, precision: 10);
    }

    [Fact]
    public void Only_items_present_in_both_runs_are_paired()
    {
        var baseline = Report(Items(0.5, 0.5, 0.5, 0.5));
        var variant = Report(new Dictionary<(string, string), double>
        {
            [("dev-001", "Top")] = 1.0,
            [("dev-001", "Right")] = 1.0,
        });

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal(2, result.PairedItemCount);
        Assert.Equal(0.5, result.DeltaRecovery, precision: 10);
    }

    [Fact]
    public void Confidence_interval_brackets_the_delta()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.5, 40).ToArray()));
        var variant = Report(Items(Enumerable.Repeat(0.6, 40).ToArray()));

        var result = PairedComparison.Compare(baseline, variant);

        Assert.True(result.CiLow <= result.DeltaRecovery);
        Assert.True(result.DeltaRecovery <= result.CiHigh);
    }

    [Fact]
    public void Bootstrap_is_deterministic_for_a_fixed_seed()
    {
        var baseline = Report(Items(0.0, 0.5, 1.0, 0.5, 0.0, 0.5, 1.0, 0.5));
        var variant = Report(Items(0.5, 1.0, 1.0, 0.5, 0.5, 0.5, 1.0, 1.0));

        var a = PairedComparison.Compare(baseline, variant, bootstrapIterations: 500, seed: 99);
        var b = PairedComparison.Compare(baseline, variant, bootstrapIterations: 500, seed: 99);

        Assert.Equal(a.CiLow, b.CiLow);
        Assert.Equal(a.CiHigh, b.CiHigh);
    }

    [Fact]
    public void A_zero_delta_yields_a_ci_containing_zero()
    {
        var items = Items(Enumerable.Repeat(0.5, 40).ToArray());
        var result = PairedComparison.Compare(Report(items), Report(items));

        Assert.Equal(0.0, result.DeltaRecovery, precision: 10);
        Assert.True(result.CiLow <= 0.0 && result.CiHigh >= 0.0);
    }

    // ---- Règle de promotion : les trois branches ------------------------------

    [Fact]
    public void Verdict_is_retenu_when_all_three_conditions_hold()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()), validRate: 0.94, boardSolvedFirstTry: 0.12);
        var variant = Report(Items(Enumerable.Repeat(0.60, 40).ToArray()), validRate: 0.94, boardSolvedFirstTry: 0.13);

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal("retenu", result.Verdict);
    }

    [Fact]
    public void Verdict_is_neutre_when_the_gain_falls_short_of_three_points()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()));
        var variant = Report(Items(Enumerable.Repeat(0.51, 40).ToArray()));

        Assert.Equal("neutre", PairedComparison.Compare(baseline, variant).Verdict);
    }

    [Fact]
    public void Verdict_is_ecarte_when_recovery_drops_by_three_points_or_more()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.60, 40).ToArray()));
        var variant = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()));

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal("écarté", result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("recovery"));
    }

    // Un prompt qui gagne en devinabilité en perdant de la validité est un régrès.
    [Fact]
    public void A_valid_rate_loss_above_one_point_disqualifies_even_a_large_recovery_gain()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()), validRate: 0.94);
        var variant = Report(Items(Enumerable.Repeat(0.70, 40).ToArray()), validRate: 0.90);

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal("écarté", result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("valid_rate"));
    }

    [Fact]
    public void A_board_solved_first_try_regression_above_five_points_disqualifies()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()), boardSolvedFirstTry: 0.20);
        var variant = Report(Items(Enumerable.Repeat(0.70, 40).ToArray()), boardSolvedFirstTry: 0.13);

        var result = PairedComparison.Compare(baseline, variant);

        Assert.Equal("écarté", result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("board_solved_first_try"));
    }

    [Fact]
    public void A_valid_rate_loss_of_exactly_one_point_is_still_acceptable()
    {
        var baseline = Report(Items(Enumerable.Repeat(0.50, 40).ToArray()), validRate: 0.94);
        var variant = Report(Items(Enumerable.Repeat(0.60, 40).ToArray()), validRate: 0.93);

        Assert.Equal("retenu", PairedComparison.Compare(baseline, variant).Verdict);
    }

    [Fact]
    public void Refuses_a_comparison_with_no_paired_item()
    {
        var baseline = Report(Items(0.5, 0.5));
        var variant = Report(new Dictionary<(string, string), double>
        {
            [("dev-999", "Top")] = 1.0,
        });

        Assert.Throws<InvalidOperationException>(() => PairedComparison.Compare(baseline, variant));
    }
}
