using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Runner;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Decoder;

public sealed record DecodeUnitContext(
    RunContents Run,
    DecodeManifest Manifest,
    string ExperimentId,
    string DatasetId,
    string Fingerprint,
    int DecodesPerClue,
    string BenchHash,
    IReadOnlyDictionary<(string BoardId, string Direction), string> ValidClues,
    IReadOnlySet<(string BoardId, string Direction, int DecodeIndex)> AlreadyDecoded);

public sealed record DecodeUnitResult(
    IReadOnlyList<ClueDecodeLine> ClueLines,
    BoardDecodeLine? BoardLine,
    IReadOnlyList<ItemSpans> Items);

/// <summary>
/// Un board de <c>decode</c> — l'unité atomique (spec phase 3, §8 bis). Chaque direction ayant au
/// moins une tentative au run devient un item d'experiment (mêmes attributs que le backfill) ; ses
/// décodages en sont les enfants, et leurs appels LLM les petits-enfants. Rien n'est écrit ici :
/// c'est à l'appelant de consigner les lignes APRÈS <see cref="TracingSession.Checkpoint"/>.
/// </summary>
public static class DecodeBoardUnit
{
    public static async Task<DecodeUnitResult> RunAsync(
        ClueDecoder clueDecoder, BoardDecoder boardDecoder, DecodeUnitContext ctx, BenchBoard board, CancellationToken ct)
    {
        var runId = ctx.Run.Manifest.RunId;
        var clueLines = new List<ClueDecodeLine>();
        var items = new List<ItemSpans>();

        foreach (var benchDirection in board.Directions)
        {
            var attempts = ctx.Run.Attempts
                .Where(a => a.BoardId == board.BoardId && a.Direction == benchDirection.Direction)
                .OrderBy(a => a.Attempt)
                .ToList();
            if (attempts.Count == 0)
                continue; // même règle que OtlpExperimentBuilder : pas de tentative, pas d'item

            var itemId = BenchDatasetMapper.ItemId(board.BoardId, benchDirection.Direction);
            using var root = EvalTracing.StartRoot("experiment-item", OtlpIds.TraceId(ctx.ExperimentId, itemId));
            var rootId = root?.SpanId.ToHexString() ?? string.Empty;
            var common = ExperimentAttributes.Common(
                ctx.ExperimentId, ctx.DatasetId, ctx.Fingerprint, ctx.Run.Manifest, ctx.Manifest, itemId, rootId).ToList();

            SetAll(root, common);
            SetAll(root, ExperimentAttributes.ItemRoot(board, benchDirection, attempts));
            root?.SetTag(EvalTracing.SessionId, runId);
            // Lien vers la trace de génération (même id déterministe que GenerateDirectionUnit).
            root?.SetTag(EvalTracing.Metadata("generate_trace_id"), OtlpIds.TraceId(runId, itemId));

            var decodeSpans = new List<DecodeSpan>();
            if (ctx.ValidClues.TryGetValue((board.BoardId, benchDirection.Direction), out var clue))
            {
                var direction = Enum.Parse<Direction>(benchDirection.Direction);
                for (var index = 0; index < ctx.DecodesPerClue; index++)
                {
                    if (ctx.AlreadyDecoded.Contains((board.BoardId, benchDirection.Direction, index)))
                        continue;

                    // Ouvert AVANT l'appel : le span `chat` en devient l'enfant (Activity.Current).
                    using var span = EvalTracing.Source.StartActivity($"decode-clue #{index}");
                    SetAll(span, common);
                    span?.SetTag(EvalTracing.SessionId, runId);
                    var line = await clueDecoder
                        .DecodeAsync(board, direction, clue, index, ctx.BenchHash, ct).ConfigureAwait(false);
                    span?.SetTag(EvalTracing.Output, line.Picked is null
                        ? $"échec : {line.DecodeFailureKind}"
                        : string.Join(" + ", line.Picked));
                    span?.SetTag(EvalTracing.Metadata("r"), line.R?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—");

                    clueLines.Add(line);
                    decodeSpans.Add(new DecodeSpan(index, span?.SpanId.ToHexString() ?? string.Empty, line.R));
                }
            }

            items.Add(new ItemSpans(itemId, OtlpIds.TraceId(ctx.ExperimentId, itemId), rootId, decodeSpans,
                attempts.Any(a => a.Valid)));
        }

        // N3 : seulement si les 4 directions ont un indice valide — une affectation partielle ne
        // mesure pas la cohérence board (règle inchangée).
        var boardClues = BoardGeometry.AllDirections
            .Where(d => ctx.ValidClues.ContainsKey((board.BoardId, d.ToString())))
            .ToDictionary(d => d, d => ctx.ValidClues[(board.BoardId, d.ToString())]);

        BoardDecodeLine? boardLine = null;
        if (boardClues.Count == 4)
        {
            using var boardRoot = EvalTracing.StartRoot("decode-board", OtlpIds.TraceId(ctx.ExperimentId, board.BoardId));
            boardRoot?.SetTag(EvalTracing.SessionId, runId);
            boardRoot?.SetTag(EvalTracing.Environment, "decode");
            boardRoot?.SetTag(EvalTracing.Metadata("decoder_fingerprint"), ctx.Fingerprint);
            boardLine = await boardDecoder.DecodeAsync(board, boardClues, ctx.BenchHash, ct).ConfigureAwait(false);
            boardRoot?.SetTag(EvalTracing.Output, boardLine.Assignment is null
                ? $"échec : {boardLine.DecodeFailureKind}"
                : new JsonObject(boardLine.Assignment.Select(kv => KeyValuePair.Create(
                        kv.Key, (JsonNode?)new JsonArray(kv.Value.Select(w => (JsonNode?)w).ToArray()))))
                    .ToJsonString(EvalJson.Options));
            boardRoot?.SetTag(EvalTracing.Metadata("board_positions"),
                boardLine.BoardPositions?.ToString("0.000", CultureInfo.InvariantCulture) ?? "—");
        }

        return new DecodeUnitResult(clueLines, boardLine, items);
    }

    private static void SetAll(Activity? activity, IEnumerable<(string Key, object Value)> attributes)
    {
        if (activity is null) return;
        foreach (var (key, value) in attributes)
            activity.SetTag(key, value);
    }
}
