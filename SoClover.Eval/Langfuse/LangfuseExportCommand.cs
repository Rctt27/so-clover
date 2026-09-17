using System.Text.Json;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Verbe <c>langfuse-export</c> : un run décodé et scoré → experiment Langfuse (spec §7.2).
/// Aucun appel LLM. Idempotent par construction : ids de traces, spans et scores déterministes.
/// Une experiment par couple (run, décodeur) — deux empreintes ne se mélangent jamais.
/// </summary>
public static class LangfuseExportCommand
{
    private const int DatasetRunPollAttempts = 10;
    private static readonly TimeSpan DatasetRunPollDelay = TimeSpan.FromSeconds(3);

    public static string ExperimentName(string runId, string fingerprint) => $"{runId}.{fingerprint}";

    public static void RequireExportable(RunContents run, DecodeContents decoded, string metricsJson)
    {
        if (decoded.Manifest.GeneratorRunId != run.Manifest.RunId)
            throw new InvalidOperationException(
                $"Le décodage désigné appartient au run {decoded.Manifest.GeneratorRunId}, pas à {run.Manifest.RunId}.");

        using var metrics = JsonDocument.Parse(metricsJson);
        if (metrics.RootElement.TryGetProperty("subsetFile", out var subset) && subset.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException(
                $"Ce run a été scoré avec --subset {subset.GetString()} : les pseudo-runs humains ne sont pas " +
                "exportés dans cette phase (spec §4, D5).");
    }

    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var runPath = args.Require("run");
        var decodedPath = args.Get("decoded")
                          ?? DecodeFile.FindForRun(runPath)
                          ?? throw new InvalidOperationException($"{runPath} n'a aucun décodage : `decode` puis `score` d'abord.");
        var metricsPath = DecodeFile.MetricsPathFor(decodedPath);
        if (!File.Exists(metricsPath))
            throw new InvalidOperationException($"{metricsPath} absent : lancer `score --run {runPath}` avant d'exporter.");

        var run = RunFile.Read(runPath);
        var decoded = DecodeFile.Read(decodedPath);
        RequireExportable(run, decoded, File.ReadAllText(metricsPath));

        var bench = BenchFile.Read(run.Manifest.BenchFile);
        BenchDatasetMapper.RequireNotTestBench(bench.Manifest);

        // Recalculées plutôt que relues : même choix que `compare`, le .metrics.json n'est qu'une synthèse.
        var metrics = RunMetrics.Compute(bench, run, decoded, run.Manifest.MaxRetries + 1);

        var fingerprint = DecoderFingerprint.FromManifest(decoded.Manifest);
        var experimentId = ExperimentName(run.Manifest.RunId, fingerprint);

        var options = EvalLlmConfig.BindLangfuse(EvalLlmConfig.BuildConfiguration());
        var client = LangfuseClientFactory.CreateRequired(options);

        var datasetName = BenchDatasetMapper.DatasetName(bench.Manifest);
        var dataset = await client.GetDatasetAsync(datasetName, ct).ConfigureAwait(false)
                      ?? throw new InvalidOperationException(
                          $"Dataset {datasetName} absent de Langfuse : lancer `langfuse-sync --bench {run.Manifest.BenchFile}` d'abord.");
        if (dataset.BenchHash != bench.Manifest.BenchHash)
            throw new InvalidOperationException(
                $"Le dataset {datasetName} porte benchHash {dataset.BenchHash}, le run {bench.Manifest.BenchHash}.");

        var export = OtlpExperimentBuilder.Build(
            new ExperimentContext(experimentId, dataset.Id, fingerprint, bench, run, decoded));

        Console.WriteLine($"experiment : {experimentId}");
        Console.WriteLine($"  dataset      : {datasetName} ({dataset.Id})");
        Console.WriteLine($"  items        : {export.Items.Count}, lots OTLP : {export.Payloads.Count}");

        foreach (var payload in export.Payloads)
            await client.SendOtlpTracesAsync(payload, ct).ConfigureAwait(false);

        var itemScores = ExperimentScores.ForItems(experimentId, export.Items);
        foreach (var score in itemScores)
            await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
        Console.WriteLine($"  scores item  : {itemScores.Count}");

        // L'ingestion OTLP est asynchrone côté Langfuse : l'experiment n'existe qu'une fois les
        // spans traités par le worker. La fenêtre couvre les horodatages reconstruits (passés).
        string? datasetRunId = null;
        for (var attempt = 0; attempt < DatasetRunPollAttempts && datasetRunId is null; attempt++)
        {
            datasetRunId = await client.FindExperimentIdAsync(
                experimentId,
                run.Manifest.CreatedAtUtc.AddDays(-1),
                DateTime.UtcNow.AddDays(1),
                ct).ConfigureAwait(false);
            if (datasetRunId is null)
                await Task.Delay(DatasetRunPollDelay, ct).ConfigureAwait(false);
        }

        if (datasetRunId is null)
        {
            Console.Error.WriteLine(
                "AVERTISSEMENT : le dataset run n'est pas encore visible, scores de run NON publiés. " +
                "Relancer la même commande (idempotente).");
            return 1;
        }

        var runScores = ExperimentScores.ForRun(experimentId, datasetRunId, metrics);
        foreach (var score in runScores)
            await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
        Console.WriteLine($"  scores run   : {string.Join(", ", runScores.Select(s => $"{s.Name}={s.Value:0.000}"))}");
        Console.WriteLine("Rappel : ces scores sont des moyennes. Δ apparié, IC et verdict restent à `compare`.");
        return 0;
    }
}
