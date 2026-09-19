using System.Text.Json.Nodes;
using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Avant le premier appel LLM : clés acceptées, puis endpoint OTLP joignable (POST vide). Une
/// panne constatée ici ne coûte rien ; la même en plein run coûterait une unité (spec §5).
/// </summary>
public static class TracePreflight
{
    public static async Task RunAsync(LangfuseClient client, CancellationToken ct)
    {
        try
        {
            await client.CheckCredentialsAsync(ct).ConfigureAwait(false);
            await client.SendOtlpTracesAsync(new JsonObject { ["resourceSpans"] = new JsonArray() }, ct).ConfigureAwait(false);
        }
        catch (LangfuseException ex)
        {
            throw new LangfuseException(
                $"Préflight du traçage en échec : {ex.Message} — démarrer tools/langfuse, ou --trace off " +
                "pour lancer sans traçage.", ex);
        }
    }
}
