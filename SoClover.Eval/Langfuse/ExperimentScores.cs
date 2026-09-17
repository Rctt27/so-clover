using SoClover.Eval.Io;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Scores publiés dans Langfuse. Aucun n'est calculé ici au-delà d'une moyenne d'item : les
/// agrégats de run sont ceux de <see cref="RunMetrics"/>, recopiés avec leur effectif. Les verdicts
/// (Δ apparié, IC, portes) restent au harnais — Langfuse les montrerait comme des moyennes.
/// </summary>
public static class ExperimentScores
{
    public static IReadOnlyList<LangfuseScore> ForItems(string experimentId, IReadOnlyList<ItemSpans> items)
    {
        var scores = new List<LangfuseScore>();
        foreach (var item in items)
        {
            var exploitable = item.Decodes.Where(d => d.R is not null).ToList();
            foreach (var decode in exploitable)
            {
                scores.Add(new LangfuseScore(
                    ScoreId(experimentId, item.ItemId, $"r-{decode.DecodeIndex}"), "r", decode.R!.Value,
                    item.TraceId, decode.SpanId, null, null));
            }

            // D6 : aucun décodage exploitable ⟹ aucun score, jamais un R̄ = 0 fabriqué.
            if (exploitable.Count == 0)
                continue;

            scores.Add(new LangfuseScore(
                ScoreId(experimentId, item.ItemId, "recovery"), "recovery",
                exploitable.Average(d => d.R!.Value),
                item.TraceId, item.RootSpanId, null,
                $"{exploitable.Count} décodage(s) exploitable(s) sur {item.Decodes.Count}"));
        }
        return scores;
    }

    public static IReadOnlyList<LangfuseScore> ForRun(string experimentId, string datasetRunId, MetricsReport metrics)
    {
        var counts = metrics.Counts;
        // Effectifs recopiés de RunMetrics.Compute : recovery = Σ R̄ / DirectionCount porte sur
        // TOUTES les directions du banc (une direction sans décodage y entre avec R̄ = 0), pas
        // seulement celles effectivement décodées — contrairement à half_rate.
        (string Name, double Value, int Denominator)[] candidates =
        [
            ("recovery", metrics.Recovery, metrics.DirectionCount),
            ("half_rate", metrics.HalfRate, counts.DecodedItems),
            ("valid_rate", metrics.ValidRate, metrics.DirectionCount),
            ("first_attempt_rate", metrics.FirstAttemptRate, metrics.DirectionCount),
            ("board_positions", metrics.BoardPositions, counts.ScoredBoards),
            ("decode_failure_rate", metrics.DecodeFailureRate, counts.Decodes),
        ];

        // Dénominateur nul ⟹ score absent : un 0,000 sur zéro item serait un chiffre fabriqué.
        return candidates
            .Where(c => c.Denominator > 0)
            .Select(c => new LangfuseScore(
                ScoreId(experimentId, "run", c.Name), c.Name, c.Value, null, null, datasetRunId, $"n = {c.Denominator}"))
            .ToList();
    }

    public static string ScoreId(string experimentId, string scope, string name) =>
        EvalJson.Sha256Hex($"score|{experimentId}|{scope}|{name}")[..32];
}
