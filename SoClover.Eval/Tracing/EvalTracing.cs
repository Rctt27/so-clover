using System.Diagnostics;
using System.Globalization;
using SoClover.Eval.Decoder;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Sources et attributs du traçage en direct (spec phase 3, §3). Les noms d'attributs
/// <c>langfuse.*</c> sont ceux qu'interprète l'endpoint OTLP de Langfuse — les mêmes que le
/// backfill (<see cref="Langfuse.OtlpExperimentBuilder"/>). Sans écouteur (<c>--trace off</c>),
/// toute activité démarrée vaut <c>null</c> : les verbes n'ont qu'un chemin de code.
/// </summary>
public static class EvalTracing
{
    public const string SourceName = "SoClover.Eval";
    public const string ChatSourceName = "SoClover.Eval.Chat";

    public const string SessionId = "langfuse.session.id";
    public const string Environment = "langfuse.environment";
    public const string Input = "langfuse.observation.input";
    public const string Output = "langfuse.observation.output";

    public static readonly ActivitySource Source = new(SourceName);

    public static string Metadata(string key) => $"langfuse.observation.metadata.{key}";

    /// <summary>
    /// Racine de trace à id <b>déterministe</b> (32 hex, <see cref="Langfuse.OtlpIds.TraceId"/>) :
    /// le contexte parent ne porte que le trace id, son span id nul fait exporter la racine sans
    /// parent (hypothèse O3). C'est ce qui permet au décodage de pointer vers la trace de génération.
    /// </summary>
    public static Activity? StartRoot(string name, string traceIdHex) =>
        Source.StartActivity(
            name,
            ActivityKind.Internal,
            new ActivityContext(ActivityTraceId.CreateFromString(traceIdHex), default, ActivityTraceFlags.Recorded));

    /// <summary>
    /// Annotation d'un span <c>decode-clue</c> : partagée par <see cref="Decoder.DecodeBoardUnit"/> et
    /// <see cref="Calibration.CalibrateClueUnit"/>, qui décodent chacun un indice puis posent les
    /// mêmes tags (sortie + <c>r</c>). Ne rien poser si le traçage est éteint.
    /// </summary>
    public static void AnnotateDecode(Activity? span, ClueDecodeLine line)
    {
        if (span is null) return;
        span.SetTag(Output, line.Picked is null
            ? $"échec : {line.DecodeFailureKind}"
            : string.Join(" + ", line.Picked));
        span.SetTag(Metadata("r"), line.R?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—");
    }
}
