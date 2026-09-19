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
    IReadOnlySet<(string BoardId, string Direction, int DecodeIndex)> AlreadyDecoded,
    // Boards dont la ligne N3 existe déjà (reprise d'un fichier antérieur au board atomique) :
    // N3 n'est pas rejoué.
    IReadOnlySet<string>? AlreadyBoardDecoded = null);

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
            var hasValidClue = ctx.ValidClues.ContainsKey((board.BoardId, benchDirection.Direction));
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
                    EvalTracing.AnnotateDecode(span, line);

                    clueLines.Add(line);
                    decodeSpans.Add(new DecodeSpan(index, span?.SpanId.ToHexString() ?? string.Empty, line.R));
                }
            }

            // Reprise partielle (M-8) : une direction à indice valide dont tous les décodages
            // étaient déjà faits cette session ne produit aucun ItemSpans — en émettre un vide
            // publierait recovery = 0 et écraserait le score déjà correct de la passe précédente.
            // Une direction A-1 (sans indice valide) garde son item avec HasValidClue = false : son
            // recovery = 0 est la mesure elle-même, pas un artefact de reprise.
            if (hasValidClue && decodeSpans.Count == 0)
                continue;

            items.Add(new ItemSpans(itemId, OtlpIds.TraceId(ctx.ExperimentId, itemId), rootId, decodeSpans,
                attempts.Any(a => a.Valid)));
        }

        // N3 : seulement si les 4 directions ont un indice valide — une affectation partielle ne
        // mesure pas la cohérence board (règle inchangée).
        var boardClues = BoardGeometry.AllDirections
            .Where(d => ctx.ValidClues.ContainsKey((board.BoardId, d.ToString())))
            .ToDictionary(d => d, d => ctx.ValidClues[(board.BoardId, d.ToString())]);

        BoardDecodeLine? boardLine = null;
        if (boardClues.Count == 4 && ctx.AlreadyBoardDecoded?.Contains(board.BoardId) != true)
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
