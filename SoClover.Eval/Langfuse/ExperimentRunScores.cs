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

    public static async Task<bool> PublishAsync(
        LangfuseClient client, string experimentId, MetricsReport metrics, DateTime from, DateTime to, CancellationToken ct)
    {
        string? datasetRunId = null;
        for (var attempt = 0; attempt < PollAttempts && datasetRunId is null; attempt++)
        {
            // Ne jamais se fier au champ `itemCount` de GET /api/public/experiments comme preuve de
            // complétude : Langfuse y compte les observations racine, pas les items distincts —
            // observé à 2 pour 1 item après une réémission de spans (Ruling 12).
            datasetRunId = await client.FindExperimentIdAsync(experimentId, from, to, ct).ConfigureAwait(false);
            if (datasetRunId is null)
                await Task.Delay(PollDelay, ct).ConfigureAwait(false);
        }
        if (datasetRunId is null)
            return false;

        var scores = ExperimentScores.ForRun(experimentId, datasetRunId, metrics);
        foreach (var score in scores)
            await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
        Console.WriteLine($"  scores run   : {string.Join(", ", scores.Select(s => $"{s.Name}={s.Value:0.000}"))}");
        return true;
    }
}
