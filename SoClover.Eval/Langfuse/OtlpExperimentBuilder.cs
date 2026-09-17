using System.Globalization;
using System.Text.Json.Nodes;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Langfuse;

public sealed record ExperimentContext(
    string ExperimentId,
    string DatasetId,
    string DecoderFingerprint,
    BenchContents Bench,
    RunContents Run,
    DecodeContents Decoded);

public sealed record DecodeSpan(int DecodeIndex, string SpanId, double? R);

/// <summary>
/// <paramref name="HasValidClue"/> : au moins une tentative valide — la même règle que
/// <see cref="SoClover.Eval.Scoring.RunMetrics"/>, qui compte R̄ = 0 aux directions sans indice valide (A-1).
/// </summary>
public sealed record ItemSpans(
    string ItemId, string TraceId, string RootSpanId, IReadOnlyList<DecodeSpan> Decodes, bool HasValidClue);

public sealed record OtlpExport(IReadOnlyList<JsonObject> Payloads, IReadOnlyList<ItemSpans> Items);

/// <summary>
/// Un run décodé → spans OTLP/JSON aux attributs d'experiment de Langfuse (spec §7.2). Construits à
/// la main, sans SDK OpenTelemetry : un backfill doit fixer lui-même ids et horodatages, ce que le
/// SDK ne permet pas. Les horodatages sont <b>reconstruits</b> : départ à la création du run, puis
/// les latences consignées bout à bout. Les durées sont exactes, les heures absolues approximatives.
/// En OTLP brut il n'y a pas de baggage : ses attributs sont posés sur chaque span.
/// </summary>
public static class OtlpExperimentBuilder
{
    public const int ItemsPerPayload = 20;

    public static OtlpExport Build(ExperimentContext ctx)
    {
        var cursor = ctx.Run.Manifest.CreatedAtUtc;
        var items = new List<(ItemSpans Item, List<JsonObject> Spans)>();

        foreach (var board in ctx.Bench.Boards)
        {
            foreach (var direction in board.Directions)
            {
                var attempts = ctx.Run.Attempts
                    .Where(a => a.BoardId == board.BoardId && a.Direction == direction.Direction)
                    .OrderBy(a => a.Attempt)
                    .ToList();
                if (attempts.Count == 0)
                    continue;

                var decodes = ctx.Decoded.ClueDecodes
                    .Where(d => d.BoardId == board.BoardId && d.Direction == direction.Direction)
                    .OrderBy(d => d.DecodeIndex)
                    .ToList();

                var itemId = BenchDatasetMapper.ItemId(board.BoardId, direction.Direction);
                var traceId = OtlpIds.TraceId(ctx.ExperimentId, itemId);
                var rootId = OtlpIds.SpanId(ctx.ExperimentId, itemId, "root");
                var common = CommonAttributes(ctx, itemId, rootId).ToList();

                var rootStart = cursor;
                var children = new List<JsonObject>();

                foreach (var attempt in attempts)
                {
                    var end = cursor.AddMilliseconds(Math.Max(1, attempt.LatencyMs));
                    var attributes = new List<(string, object)>(common)
                    {
                        ("langfuse.observation.type", "generation"),
                        ("gen_ai.request.model", attempt.EffectiveModel),
                        ("langfuse.observation.output", attempt.Clue ?? $"échec : {attempt.FailureKind}"),
                        ("langfuse.observation.metadata.valid", attempt.Valid ? "true" : "false"),
                        ("langfuse.observation.metadata.rejection_rules", string.Join(",", attempt.RejectionRules)),
                    };
                    if (attempt.InputTokens is { } input) attributes.Add(("gen_ai.usage.input_tokens", input));
                    if (attempt.OutputTokens is { } output) attributes.Add(("gen_ai.usage.output_tokens", output));

                    children.Add(Span(traceId, OtlpIds.SpanId(ctx.ExperimentId, itemId, $"gen-{attempt.Attempt}"),
                        rootId, $"generate attempt {attempt.Attempt}", cursor, end, attributes));
                    cursor = end;
                }

                var decodeSpans = new List<DecodeSpan>();
                foreach (var decode in decodes)
                {
                    var end = cursor.AddMilliseconds(Math.Max(1, decode.LatencyMs));
                    var spanId = OtlpIds.SpanId(ctx.ExperimentId, itemId, $"decode-{decode.DecodeIndex}");
                    var attributes = new List<(string, object)>(common)
                    {
                        ("langfuse.observation.type", "generation"),
                        ("gen_ai.request.model", ctx.Decoded.Manifest.ModelId),
                        ("langfuse.observation.output", decode.Picked is null
                            ? $"échec : {decode.DecodeFailureKind}"
                            : string.Join(" + ", decode.Picked)),
                        ("langfuse.observation.metadata.r", decode.R?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—"),
                    };

                    children.Add(Span(traceId, spanId, rootId, $"decode-clue #{decode.DecodeIndex}", cursor, end, attributes));
                    decodeSpans.Add(new DecodeSpan(decode.DecodeIndex, spanId, decode.R));
                    cursor = end;
                }

                var retained = attempts.FirstOrDefault(a => a.Valid);
                var rootAttributes = new List<(string, object)>(common)
                {
                    ("langfuse.observation.input", BenchDatasetMapper.Input(board, direction.Direction).ToJsonString()),
                    ("langfuse.observation.output", new JsonObject
                    {
                        ["clue"] = retained?.Clue,
                        ["valid"] = retained is not null,
                        ["attempts"] = attempts.Count,
                    }.ToJsonString()),
                    ("langfuse.experiment.item.expected_output", new JsonObject
                    {
                        ["referenceWords"] = new JsonArray(direction.ReferenceWords.Select(w => (JsonNode?)w).ToArray()),
                    }.ToJsonString()),
                };

                var root = Span(traceId, rootId, null, "experiment-item", rootStart, cursor, rootAttributes);
                items.Add((new ItemSpans(itemId, traceId, rootId, decodeSpans, retained is not null), children.Prepend(root).ToList()));
            }
        }

        var payloads = items
            .Chunk(ItemsPerPayload)
            .Select(chunk => Payload(chunk.SelectMany(i => i.Spans)))
            .ToList();

        return new OtlpExport(payloads, items.Select(i => i.Item).ToList());
    }

    public static string UnixNanos(DateTime utc)
    {
        var instant = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
        return ((instant.Ticks - DateTime.UnixEpoch.Ticks) * 100).ToString(CultureInfo.InvariantCulture);
    }

    private static IEnumerable<(string Key, object Value)> CommonAttributes(ExperimentContext ctx, string itemId, string rootId)
    {
        var run = ctx.Run.Manifest;
        var decoded = ctx.Decoded.Manifest;
        yield return ("langfuse.experiment.id", ctx.ExperimentId);
        yield return ("langfuse.experiment.name", ctx.ExperimentId);
        yield return ("langfuse.experiment.dataset.id", ctx.DatasetId);
        yield return ("langfuse.environment", "experiment");
        yield return ("langfuse.experiment.metadata.run_id", run.RunId);
        yield return ("langfuse.experiment.metadata.bench_hash", run.BenchHash);
        yield return ("langfuse.experiment.metadata.decoder_fingerprint", ctx.DecoderFingerprint);
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

    private static JsonObject Span(
        string traceId, string spanId, string? parentSpanId, string name,
        DateTime start, DateTime end, IEnumerable<(string Key, object Value)> attributes)
    {
        var span = new JsonObject
        {
            ["traceId"] = traceId,
            ["spanId"] = spanId,
            ["name"] = name,
            ["kind"] = 1,
            ["startTimeUnixNano"] = UnixNanos(start),
            ["endTimeUnixNano"] = UnixNanos(end),
            ["attributes"] = new JsonArray(attributes.Select(Attribute).ToArray()),
        };
        if (parentSpanId is not null)
            span["parentSpanId"] = parentSpanId;
        return span;
    }

    private static JsonNode? Attribute((string Key, object Value) attribute) => new JsonObject
    {
        ["key"] = attribute.Key,
        ["value"] = attribute.Value switch
        {
            long l => new JsonObject { ["intValue"] = l.ToString(CultureInfo.InvariantCulture) },
            _ => new JsonObject { ["stringValue"] = Convert.ToString(attribute.Value, CultureInfo.InvariantCulture) },
        },
    };

    private static JsonObject Payload(IEnumerable<JsonObject> spans) => new()
    {
        ["resourceSpans"] = new JsonArray(new JsonObject
        {
            ["resource"] = new JsonObject
            {
                ["attributes"] = new JsonArray(new JsonObject
                {
                    ["key"] = "service.name",
                    ["value"] = new JsonObject { ["stringValue"] = "soclover-eval" },
                }),
            },
            ["scopeSpans"] = new JsonArray(new JsonObject
            {
                ["scope"] = new JsonObject { ["name"] = "soclover-eval.langfuse-export" },
                ["spans"] = new JsonArray(spans.Select(s => (JsonNode?)s).ToArray()),
            }),
        }),
    };
}
