using System.Text.Json;
using SoClover.Eval.Bench;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Verbe <c>langfuse-export</c> : un run décodé et scoré → experiment Langfuse (spec §7.2).
/// Aucun appel LLM. Idempotent par construction : ids de traces, spans et scores déterministes.
/// Une experiment par couple (run, décodeur) — deux empreintes ne se mélangent jamais.
/// </summary>
public static class LangfuseExportCommand
{
    public static string ExperimentName(string runId, string fingerprint) => $"{runId}.{fingerprint}";

    /// <summary>
    /// Une experiment backfillée commence à la création du run ; une experiment tracée en direct
    /// (phase 3) à celle du décodage. La fenêtre couvre les deux : manquer l'experiment ferait
    /// renvoyer ses spans, donc les dupliquer (Ruling 12).
    /// </summary>
    public static (DateTime From, DateTime To) SearchWindow(DateTime runCreatedAtUtc, DateTime decodeCreatedAtUtc) =>
        (Min(runCreatedAtUtc, decodeCreatedAtUtc).AddDays(-1), Max(runCreatedAtUtc, decodeCreatedAtUtc).AddDays(1));

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    /// <summary>
    /// Ruling 12 (2026-09-17) : sur ce déploiement Langfuse v4 « events_only », renvoyer les mêmes
    /// spans OTLP à une experiment déjà ingérée ne les met pas à jour — ça les duplique. Constaté en
    /// pratique le 2026-09-17 sur la trace <c>473ef7c265bd585074320c5a472c0884</c> : un second export
    /// identique a fait passer <c>GET /api/public/v2/observations?traceId=…</c> de 5 à 10 lignes
    /// strictement identiques (mêmes ids, mêmes horodatages). Les ids OTLP restent déterministes
    /// (hypothèse H3, <see cref="OtlpIds"/>) mais ce déploiement ne les traite pas comme une clé
    /// d'upsert pour les observations — contrairement aux scores, dont l'idempotence par id est
    /// documentée et vérifiée pour <see cref="LangfuseClient.CreateScoreAsync"/>. Donc : ne renvoyer
    /// les spans que si l'experiment n'existe pas encore, ou si l'opérateur force explicitement le
    /// doublon via <c>--resend-spans</c> en connaissance de cause. Les scores, eux, sont toujours
    /// republiés — c'est ce qui permet de compléter un export dont les scores de run avaient manqué
    /// (experiment pas encore visible lors du premier passage).
    /// </summary>
    internal static bool ShouldSendSpans(string? existingExperimentId, bool resendSpans) =>
        existingExperimentId is null || resendSpans;

    /// <summary>
    /// Dataset Langfuse du banc, exigé présent et au même <c>benchHash</c>. Partagé avec
    /// <c>decode</c> tracé, qui le résout avant tout appel LLM.
    /// </summary>
    internal static async Task<LangfuseDataset> RequireDatasetAsync(
        LangfuseClient client, BenchContents bench, string benchFile, CancellationToken ct)
    {
        var datasetName = BenchDatasetMapper.DatasetName(bench.Manifest);
        var dataset = await client.GetDatasetAsync(datasetName, ct).ConfigureAwait(false)
                      ?? throw new InvalidOperationException(
                          $"Dataset {datasetName} absent de Langfuse : lancer `langfuse-sync --bench {benchFile}` d'abord.");
        if (dataset.BenchHash != bench.Manifest.BenchHash)
            throw new InvalidOperationException(
                $"Le dataset {datasetName} porte benchHash {dataset.BenchHash}, le run {bench.Manifest.BenchHash}.");
        return dataset;
    }

    /// <summary>
    /// Décodage tracé en direct (phase 3) : son experiment a été créée par <c>decode</c>, avec ses
    /// propres ids de spans, et ses scores d'item publiés board par board. Le backfill ne s'y
    /// applique pas — ses ids déterministes (<see cref="OtlpIds.SpanId"/>) n'y existent pas, et ses
    /// scores d'item, de mêmes ids, écraseraient les scores live en les rattachant à des
    /// observations inexistantes. Seuls les scores de run sont (re)publiés.
    /// </summary>
    internal static bool IsLiveTraced(DecodeManifest manifest) => manifest.Tracing?.IsOn == true;

    /// <summary>
    /// <c>--resend-spans</c> n'a pas de sens pour une experiment créée en direct : renvoyer des spans
    /// la dupliquerait (Ruling 12), avec des ids qui ne sont pas ceux des observations live.
    /// </summary>
    internal static void RequireResendAllowed(DecodeManifest manifest, bool resendSpans)
    {
        if (resendSpans && IsLiveTraced(manifest))
            throw new InvalidOperationException(
                "--resend-spans refusé : ce décodage a été tracé en direct, son experiment a été créée par " +
                "`decode`. Renvoyer des spans la dupliquerait (Ruling 12). Relancer sans --resend-spans : " +
                "seuls les scores de run seront republiés.");
    }

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
        RequireResendAllowed(decoded.Manifest, args.Has("resend-spans"));

        var bench = BenchFile.Read(run.Manifest.BenchFile);
        BenchDatasetMapper.RequireNotTestBench(bench.Manifest);

        // Recalculées plutôt que relues : même choix que `compare`, le .metrics.json n'est qu'une synthèse.
        var metrics = RunMetrics.Compute(bench, run, decoded, run.Manifest.MaxRetries + 1);

        var fingerprint = DecoderFingerprint.FromManifest(decoded.Manifest);
        var experimentId = ExperimentName(run.Manifest.RunId, fingerprint);

        var options = EvalLlmConfig.BindLangfuse(EvalLlmConfig.BuildConfiguration());
        var client = LangfuseClientFactory.CreateRequired(options);

        var datasetName = BenchDatasetMapper.DatasetName(bench.Manifest);
        var dataset = await RequireDatasetAsync(client, bench, run.Manifest.BenchFile, ct).ConfigureAwait(false);

        if (IsLiveTraced(decoded.Manifest))
            return await PublishLiveRunScoresAsync(client, run, decoded, experimentId, metrics, ct).ConfigureAwait(false);

        var export = OtlpExperimentBuilder.Build(
            new ExperimentContext(experimentId, dataset.Id, fingerprint, bench, run, decoded));

        Console.WriteLine($"experiment : {experimentId}");
        Console.WriteLine($"  dataset      : {datasetName} ({dataset.Id})");
        Console.WriteLine($"  items        : {export.Items.Count}, lots OTLP : {export.Payloads.Count}");

        // Recherchée AVANT tout envoi : c'est cette recherche qui décide si les spans partent ou
        // non (ShouldSendSpans). Fenêtre couvrant à la fois la création du run et celle du décodage
        // (SearchWindow) : un backfill démarre ses spans à CreatedAtUtc du run, une trace live (phase 3)
        // à celle du décodage.
        var (searchFrom, searchTo) = SearchWindow(run.Manifest.CreatedAtUtc, decoded.Manifest.CreatedAtUtc);
        var existingExperimentId = await client.FindExperimentIdAsync(
            experimentId, searchFrom, searchTo, ct).ConfigureAwait(false);

        var datasetRunId = existingExperimentId;
        if (ShouldSendSpans(existingExperimentId, args.Has("resend-spans")))
        {
            foreach (var payload in export.Payloads)
                await client.SendOtlpTracesAsync(payload, ct).ConfigureAwait(false);

            // L'ingestion OTLP est asynchrone côté Langfuse : attendre que l'experiment soit visible
            // AVANT de poster les scores d'item, qui référencent des observations tout juste
            // envoyées — même fenêtre que la recherche pré-envoi.
            datasetRunId = await ExperimentRunScores.WaitForExperimentAsync(
                client, experimentId, searchFrom, searchTo, ct).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine(
                $"  spans        : experiment déjà présente ({existingExperimentId}), spans non renvoyés — " +
                "--resend-spans pour forcer (duplique les observations)");
        }

        // Les scores, contrairement aux spans, sont idempotents par id (LangfuseClient.CreateScoreAsync) :
        // toujours republiés, y compris quand les spans ne le sont pas — c'est ce qui permet de
        // compléter un export dont les scores de run avaient manqué la première fois.
        var itemScores = ExperimentScores.ForItems(experimentId, export.Items);
        foreach (var score in itemScores)
            await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
        Console.WriteLine($"  scores item  : {itemScores.Count}");

        if (datasetRunId is null)
        {
            Console.Error.WriteLine(
                "AVERTISSEMENT : le dataset run n'est pas encore visible, scores de run NON publiés. " +
                "Relancer la même commande (idempotente).");
            return 1;
        }

        await ExperimentRunScores.PublishAsync(client, experimentId, datasetRunId, metrics, ct).ConfigureAwait(false);
        Console.WriteLine("Rappel : ces scores sont des moyennes. Δ apparié, IC et verdict restent à `compare`.");
        return 0;
    }

    /// <summary>
    /// Export d'un décodage tracé en direct : ni spans ni scores d'item (publiés en direct par
    /// <c>decode</c>), seulement les scores de run, une fois l'experiment visible.
    /// </summary>
    private static async Task<int> PublishLiveRunScoresAsync(
        LangfuseClient client, RunContents run, DecodeContents decoded, string experimentId,
        MetricsReport metrics, CancellationToken ct)
    {
        Console.WriteLine($"experiment : {experimentId} (tracée en direct par `decode`)");
        Console.WriteLine("  spans        : non envoyés — experiment créée en direct");
        Console.WriteLine("  scores item  : laissés tels que publiés en direct par `decode`");

        var (searchFrom, searchTo) = SearchWindow(run.Manifest.CreatedAtUtc, decoded.Manifest.CreatedAtUtc);
        if (!await ExperimentRunScores.PublishAsync(client, experimentId, metrics, searchFrom, searchTo, ct).ConfigureAwait(false))
        {
            Console.Error.WriteLine(
                "AVERTISSEMENT : le dataset run n'est pas encore visible, scores de run NON publiés. " +
                "Relancer la même commande (idempotente).");
            return 1;
        }

        Console.WriteLine("Rappel : ces scores sont des moyennes. Δ apparié, IC et verdict restent à `compare`.");
        return 0;
    }
}
