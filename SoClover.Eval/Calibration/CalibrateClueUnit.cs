using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Un indice du lot de <c>calibrate</c> — l'unité atomique (spec phase 3, §4). Trace libre dans la
/// session de la calibration : ni experiment ni score, l'accord et les portes restent au harnais.
/// </summary>
public static class CalibrateClueUnit
{
    public static async Task<IReadOnlyList<ClueDecodeLine>> RunAsync(
        ClueDecoder decoder, BenchBoard board, string direction, string clue, int decodesPerClue,
        IReadOnlySet<(string BoardId, string Direction, string Clue, int DecodeIndex)> alreadyDecoded,
        string calibrationId, string fingerprint, double epsilon, string benchHash, CancellationToken ct)
    {
        var lines = new List<ClueDecodeLine>();
        using var root = EvalTracing.StartRoot(
            "calibrate-clue", OtlpIds.TraceId(calibrationId, $"{board.BoardId}-{direction}-{clue}"));
        root?.SetTag(EvalTracing.SessionId, calibrationId);
        root?.SetTag(EvalTracing.Environment, "calibration");
        root?.SetTag(EvalTracing.Input, $"{board.BoardId} {direction} : {clue}");
        root?.SetTag(EvalTracing.Metadata("decoder_fingerprint"), fingerprint);
        root?.SetTag(EvalTracing.Metadata("epsilon"), epsilon.ToString("0.###", CultureInfo.InvariantCulture));

        var parsed = Enum.Parse<Direction>(direction);
        for (var index = 0; index < decodesPerClue; index++)
        {
            if (alreadyDecoded.Contains((board.BoardId, direction, clue, index)))
                continue;

            using var span = EvalTracing.Source.StartActivity($"decode-clue #{index}");
            span?.SetTag(EvalTracing.SessionId, calibrationId);
            var line = await decoder.DecodeAsync(board, parsed, clue, index, benchHash, ct).ConfigureAwait(false);
            EvalTracing.AnnotateDecode(span, line);
            lines.Add(line);
        }
        return lines;
    }
}
