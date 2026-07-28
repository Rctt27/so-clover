using System.Globalization;
using SoClover.Eval.Cli;
using SoClover.Eval.Io;

namespace SoClover.Eval.Scoring;

/// <summary>
/// Verbe <c>compare</c> : Δ<c>recovery</c> apparié entre deux runs décodés du même banc,
/// avec son IC bootstrap et le verdict de la règle de promotion. Aucun appel LLM.
/// </summary>
public static class CompareCommand
{
    public static Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var baseline = Load(args.Require("baseline"));
        var variant = Load(args.Require("variant"));

        var result = PairedComparison.Compare(
            baseline, variant,
            bootstrapIterations: args.GetInt("bootstrap", PairedComparison.DefaultBootstrapIterations),
            seed: args.GetLong("seed", PairedComparison.DefaultBootstrapSeed));

        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));
        static string Pts(double v) => (v * 100).ToString("+0.0;-0.0;0.0", CultureInfo.GetCultureInfo("fr-FR"));

        Console.WriteLine();
        Console.WriteLine($"comparaison appariée sur {result.PairedItemCount} item(s), banc {baseline.BenchHash}");
        Console.WriteLine($"  baseline : {baseline.RunId}   recovery {N(result.BaselineRecovery)}");
        Console.WriteLine($"  variante : {variant.RunId}   recovery {N(result.VariantRecovery)}");
        Console.WriteLine();
        Console.WriteLine($"  Δ recovery      {Pts(result.DeltaRecovery)} pts   IC 95 % [{Pts(result.CiLow)} ; {Pts(result.CiHigh)}]");
        Console.WriteLine($"  Δ valid_rate    {Pts(result.DeltaValidRate)} pts");
        Console.WriteLine($"  Δ board_solved  {Pts(result.DeltaBoardSolved)} pts");
        Console.WriteLine();
        Console.WriteLine($"  verdict : {result.Verdict.ToUpperInvariant()}");
        foreach (var reason in result.Reasons)
            Console.WriteLine($"    — {reason}");
        Console.WriteLine();

        return Task.FromResult(0);
    }

    /// <summary>
    /// Accepte soit un <c>.decoded.jsonl</c>, soit le <c>.jsonl</c> du run générateur — dans les
    /// deux cas on recalcule les métriques plutôt que de relire un <c>.metrics.json</c> qui
    /// pourrait dater d'une version antérieure du scorer.
    /// </summary>
    private static MetricsReport Load(string path)
    {
        var runPath = path.EndsWith(".decoded.jsonl", StringComparison.OrdinalIgnoreCase)
            ? path[..^".decoded.jsonl".Length] + ".jsonl"
            : path;

        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);
        var decoded = DecodeFile.ReadOrNull(DecodeFile.PathFor(runPath));

        return RunMetrics.Compute(bench, run, decoded, run.Manifest.MaxRetries + 1);
    }
}
