using System.Globalization;
using System.Text.Json.Nodes;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Attributs d'experiment Langfuse (spec §7.2), partagés entre le backfill (<c>langfuse-export</c>)
/// et le futur décodage tracé en direct : mêmes clés, mêmes valeurs, quelle que soit la source des
/// horodatages.
/// </summary>
public static class ExperimentAttributes
{
    public static IEnumerable<(string Key, object Value)> Common(
        string experimentId, string datasetId, string fingerprint,
        RunManifest run, DecodeManifest decoded, string itemId, string rootId)
    {
        yield return ("langfuse.experiment.id", experimentId);
        yield return ("langfuse.experiment.name", experimentId);
        yield return ("langfuse.experiment.dataset.id", datasetId);
        yield return (EvalTracing.Environment, "experiment");
        yield return ("langfuse.experiment.metadata.run_id", run.RunId);
        yield return ("langfuse.experiment.metadata.bench_hash", run.BenchHash);
        yield return ("langfuse.experiment.metadata.decoder_fingerprint", fingerprint);
        yield return ("langfuse.experiment.metadata.generator_model", run.ModelId);
        yield return ("langfuse.experiment.metadata.generator_prompt_version", run.PromptVersion?.ToString(CultureInfo.InvariantCulture) ?? "—");
        yield return ("langfuse.experiment.metadata.generator_temperature", run.Temperature.ToString("R", CultureInfo.InvariantCulture));
        yield return ("langfuse.experiment.metadata.decoder_model", decoded.ModelId);
        yield return ("langfuse.experiment.metadata.decoder_clue_prompt_version", decoded.CluePromptVersion?.ToString(CultureInfo.InvariantCulture) ?? "—");
        yield return ("langfuse.experiment.metadata.decodes_per_clue", decoded.DecodesPerClue.ToString(CultureInfo.InvariantCulture));
        yield return ("langfuse.experiment.metadata.notes", run.OperatorNotes ?? "—");
        yield return ("langfuse.experiment.item.id", itemId);
        yield return ("langfuse.experiment.item.root_observation_id", rootId);
    }

    public static IEnumerable<(string Key, object Value)> ItemRoot(
        BenchBoard board, BenchDirection direction, IReadOnlyList<RunAttempt> attempts)
    {
        var retained = attempts.FirstOrDefault(a => a.Valid);
        yield return (EvalTracing.Input, BenchDatasetMapper.Input(board, direction.Direction).ToJsonString(EvalJson.Options));
        yield return (EvalTracing.Output, new JsonObject
        {
            ["clue"] = retained?.Clue,
            ["valid"] = retained is not null,
            ["attempts"] = attempts.Count,
        }.ToJsonString(EvalJson.Options));
        yield return ("langfuse.experiment.item.expected_output", new JsonObject
        {
            ["referenceWords"] = new JsonArray(direction.ReferenceWords.Select(w => (JsonNode?)w).ToArray()),
        }.ToJsonString(EvalJson.Options));
    }
}
