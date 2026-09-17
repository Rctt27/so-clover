using SoClover.Eval.Io;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Identifiants OTLP <b>déterministes</b> : ré-exporter un run réécrit les mêmes traces au lieu d'en
/// ajouter (hypothèse H3). Un id aléatoire ferait de chaque relance un doublon dans Langfuse.
/// </summary>
public static class OtlpIds
{
    public static string TraceId(string experimentId, string itemId) =>
        EvalJson.Sha256Hex($"trace|{experimentId}|{itemId}")[..32];

    public static string SpanId(string experimentId, string itemId, string key) =>
        EvalJson.Sha256Hex($"span|{experimentId}|{itemId}|{key}")[..16];
}
