using System.Globalization;
using SoClover.Eval.Cli;
using SoClover.Eval.Io;

namespace SoClover.Eval.Scoring;

/// <summary>Verbe <c>score</c> : aucun appel LLM, uniquement de la lecture de fichiers.</summary>
public static class ScoreCommand
{
    public static Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var runPath = args.Require("run");
        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);
        var decoded = DecodeFile.ReadOrNull(args.Get("decoded") ?? DecodeFile.PathFor(runPath));

        var maxAttempts = run.Manifest.MaxRetries + 1;
        var metrics = RunMetrics.Compute(bench, run, decoded, maxAttempts);

        Print(metrics, run.Manifest.OperatorNotes, decoded?.Manifest.ModelId);

        // PerItemRBar est [JsonIgnore] : le détail par item se recalcule depuis les fichiers
        // de run, et n'a pas à figurer dans l'artefact de synthèse.
        var metricsPath = Path.ChangeExtension(runPath, ".metrics.json");
        File.WriteAllText(metricsPath, EvalJson.Serialize(metrics));
        Console.WriteLine($"métriques écrites : {metricsPath}");

        if (args.Get("ledger") is { } ledgerPath)
        {
            var settings = string.Create(CultureInfo.InvariantCulture,
                $"temp {run.Manifest.Temperature} / topP {run.Manifest.TopP?.ToString() ?? "—"} / " +
                $"maxTokens {run.Manifest.MaxOutputTokens?.ToString() ?? "—"} / " +
                $"maxRetries {run.Manifest.MaxRetries} / reasoning {run.Manifest.ReasoningEnabled}");

            LedgerWriter.Append(ledgerPath, new LedgerEntry(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                RunId: run.Manifest.RunId,
                BenchFile: run.Manifest.BenchFile,
                PromptFile: run.Manifest.PromptFile,
                PromptVersion: run.Manifest.PromptVersion,
                ModelId: run.Manifest.ModelId,
                ModelSnapshotDate: run.Manifest.ModelSnapshotDate,
                Settings: settings,
                Metrics: metrics,
                Status: LedgerWriter.PreCalibrationStatus,
                Hypothesis: args.Get("hypothesis"),
                Decision: args.Get("decision") ?? "neutre",
                OperatorNotes: run.Manifest.OperatorNotes));

            Console.WriteLine($"ligne ajoutée au registre : {ledgerPath}");
        }

        return Task.FromResult(0);
    }

    private static void Print(MetricsReport m, string? operatorNotes, string? decoderModel)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        Console.WriteLine();
        Console.WriteLine($"run   : {m.RunId}");
        Console.WriteLine($"banc  : {m.BenchFile} ({m.BoardCount} boards, {m.DirectionCount} directions, hash {m.BenchHash})");
        if (decoderModel is not null) Console.WriteLine($"décodeur : {decoderModel}");
        if (operatorNotes is not null) Console.WriteLine($"notes : {operatorNotes}");
        Console.WriteLine();
        Console.WriteLine("N1 — validité");
        Console.WriteLine($"  valid_rate           {N(m.ValidRate)}");
        Console.WriteLine($"  first_attempt_rate   {N(m.FirstAttemptRate)}");
        Console.WriteLine($"  parse_failure_rate   {N(m.ParseFailureRate)}");
        Console.WriteLine();
        Console.WriteLine("N2 — devinabilité");
        Console.WriteLine($"  recovery             {N(m.Recovery)}   ← métrique principale");
        Console.WriteLine($"  strict_2of2          {N(m.Strict2Of2)}");
        Console.WriteLine($"  half_rate            {N(m.HalfRate)}");
        Console.WriteLine();
        Console.WriteLine("N3 — cohérence board");
        Console.WriteLine($"  board_positions      {N(m.BoardPositions)}");
        Console.WriteLine($"  board_solved         {N(m.BoardSolved)}");
        Console.WriteLine();
        Console.WriteLine("santé");
        Console.WriteLine($"  decode_failure_rate  {N(m.DecodeFailureRate)}");
        Console.WriteLine($"  items                {m.ItemsCompleted} / {m.ItemsExpected}");

        if (m.ItemsCompleted < m.ItemsExpected)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  ⚠ RUN PARTIEL : {m.ItemsCompleted}/{m.ItemsExpected} directions terminées. " +
                "Un chiffre calculé sur un banc incomplet doit être visiblement suspect, pas publié.");
        }

        if (m.DecodeFailureRate > 0.05)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  ⚠ decode_failure_rate = {N(m.DecodeFailureRate)} > 0,05 : le prompt décodeur est cassé, " +
                "aucun recovery n'est lisible.");
        }

        if (m.ConfusionTop.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("confusion_top (mots non-référence les plus choisis)");
            foreach (var entry in m.ConfusionTop)
                Console.WriteLine($"  {entry.Count,4}  {entry.Word}");
        }
        Console.WriteLine();
    }
}
