using SoClover.Eval.Scoring;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Scores de run d'une experiment : attend qu'elle soit visible (ingestion OTLP asynchrone), puis
/// publie les agrégats de <see cref="RunMetrics"/>. Partagé par <c>langfuse-export</c> et par
/// <c>decode</c> tracé.
/// </summary>
public static class ExperimentRunScores
{
    private const int PollAttempts = 10;
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Attend que l'experiment soit visible côté Langfuse (ingestion OTLP asynchrone). Utilisé aussi
    /// bien avant de publier les scores de run que — côté <c>langfuse-export</c>, sur le chemin d'un
    /// premier envoi — avant de poster les scores d'item, qui référencent des observations tout
    /// juste envoyées : rien ne garantit qu'un score posté sur une observation pas encore ingérée
    /// soit accepté.
    /// </summary>
    public static Task<string?> WaitForExperimentAsync(
        LangfuseClient client, string experimentId, DateTime from, DateTime to, CancellationToken ct) =>
        WaitForExperimentAsync(client, experimentId, from, to, PollDelay, ct);

    /// <summary>Surcharge à délai injectable, pour les tests — la production passe toujours par <see cref="PollDelay"/>.</summary>
    internal static async Task<string?> WaitForExperimentAsync(
        LangfuseClient client, string experimentId, DateTime from, DateTime to, TimeSpan delay, CancellationToken ct)
    {
        string? datasetRunId = null;
        for (var attempt = 0; attempt < PollAttempts && datasetRunId is null; attempt++)
        {
            // Ne jamais se fier au champ `itemCount` de GET /api/public/experiments comme preuve de
            // complétude : Langfuse y compte les observations racine, pas les items distincts —
            // observé à 2 pour 1 item après une réémission de spans (Ruling 12).
            datasetRunId = await client.FindExperimentIdAsync(experimentId, from, to, ct).ConfigureAwait(false);
            if (datasetRunId is null)
                await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        return datasetRunId;
    }

    /// <summary>Publie les scores de run d'une experiment déjà localisée — aucune attente.</summary>
    public static async Task PublishAsync(
        LangfuseClient client, string experimentId, string datasetRunId, MetricsReport metrics, CancellationToken ct)
    {
        var scores = ExperimentScores.ForRun(experimentId, datasetRunId, metrics);
        foreach (var score in scores)
            await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
        Console.WriteLine($"  scores run   : {string.Join(", ", scores.Select(s => $"{s.Name}={s.Value:0.000}"))}");
    }

    /// <summary>Attend l'experiment puis publie ses scores de run ; rend <c>false</c> si elle n'est jamais visible.</summary>
    public static async Task<bool> PublishAsync(
        LangfuseClient client, string experimentId, MetricsReport metrics, DateTime from, DateTime to, CancellationToken ct)
    {
        var datasetRunId = await WaitForExperimentAsync(client, experimentId, from, to, ct).ConfigureAwait(false);
        if (datasetRunId is null)
            return false;

        await PublishAsync(client, experimentId, datasetRunId, metrics, ct).ConfigureAwait(false);
        return true;
    }
}
