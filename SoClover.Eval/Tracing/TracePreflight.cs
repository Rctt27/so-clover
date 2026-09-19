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
            // Cas injoignable (LangfuseClient.SendAsync, ~L236) : son message répète déjà
            // « tools/langfuse » et suggère --prompt-source (hors-sujet ici, c'est le repli du
            // chemin prompts). On en reprend la seule cause (l'exception réseau d'origine),
            // pas le message déjà enrichi. Cas 401/non-2xx : ex.Message est déjà sobre, on le
            // garde tel quel — il ne mentionne ni tools/langfuse ni --prompt-source.
            var cause = ex.InnerException is HttpRequestException or TaskCanceledException
                ? ex.InnerException.Message
                : ex.Message;
            throw new LangfuseException(
                $"Préflight du traçage en échec : {cause} — démarrer tools/langfuse (docker compose up -d), " +
                "ou --trace off pour lancer sans traçage.", ex);
        }
    }
}
