using System.Text.Json;
using SoClover.Infrastructure.AI;

namespace SoClover.Eval.Config;

/// <summary>
/// Ce que le serveur rapporte du modèle <b>chargé</b> — par opposition à ce que la requête demande.
/// </summary>
public sealed record ModelRuntimeInfo(string? Quantization, int? LoadedContextLength)
{
    public static readonly ModelRuntimeInfo Unknown = new(null, null);
}

/// <summary>
/// Sonde l'API native de LM Studio (<c>/api/v0/models</c>) pour consigner la quantification et la
/// longueur de contexte effectivement chargée.
/// <para>
/// Motif : <c>ChatOptions</c> ne transmet que le modèle, la température, <c>topP</c> et
/// <c>maxOutputTokens</c>. Tout le reste — quantification, contexte, <c>top_k</c>,
/// <c>repeat_penalty</c>, <c>min_p</c> — provient des réglages LM Studio, qui sont <b>par
/// modèle</b>. Deux calibrations pouvaient donc différer par la seule quantification sans qu'aucun
/// artefact ne l'enregistre, exactement comme le toggle « enable thinking ».
/// </para>
/// <para>
/// Ces champs restent <b>hors de <see cref="Calibration.DecoderFingerprint"/></b> : l'empreinte dit
/// ce qu'on a <i>demandé</i> au décodeur, ceux-ci disent ce que la machine a <i>servi</i>. Les y
/// ajouter invaliderait tous les artefacts déjà produits.
/// </para>
/// <para>
/// La sonde ne referme pas tout l'angle mort : <c>top_k</c>, <c>repeat_penalty</c> et
/// <c>min_p</c> ne sont exposés par aucun endpoint. Ils restent du ressort d'<c>operatorNotes</c>.
/// </para>
/// </summary>
public static class ModelRuntimeProbe
{
    /// <summary>
    /// <c>BaseUrl</c> pointe l'API compatible OpenAI (<c>…/v1</c>) ; ces champs ne vivent que sur
    /// l'API native. La racine se dérive en retirant le segment de version s'il est présent.
    /// </summary>
    public static string EndpointFor(string baseUrl)
    {
        var root = baseUrl.TrimEnd('/');
        if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            root = root[..^3];

        return root.TrimEnd('/') + "/api/v0/models";
    }

    /// <summary>
    /// Extraction pure, testable sans réseau. Tout payload inexploitable rend
    /// <see cref="ModelRuntimeInfo.Unknown"/> : une information d'audit absente ne doit jamais
    /// interrompre un décodage.
    /// </summary>
    public static ModelRuntimeInfo Parse(string payload, string modelId)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return ModelRuntimeInfo.Unknown;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
                return ModelRuntimeInfo.Unknown;

            foreach (var entry in data.EnumerateArray())
            {
                if (!entry.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String
                    || !string.Equals(id.GetString(), modelId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return new ModelRuntimeInfo(
                    ReadString(entry, "quantization"),
                    ReadInt(entry, "loaded_context_length"));
            }

            return ModelRuntimeInfo.Unknown;
        }
        catch (JsonException)
        {
            return ModelRuntimeInfo.Unknown;
        }
    }

    private static string? ReadString(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Absent quand le modèle n'est pas chargé. On ne se rabat <b>pas</b> sur
    /// <c>max_context_length</c> : celui-ci décrit une capacité, pas la configuration effective.
    /// </summary>
    private static int? ReadInt(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Comme <c>FetchProviderModelListHashAsync</c> : jamais fatal. Un provider distant
    /// (Anthropic) n'expose pas cet endpoint, et l'absence d'une information d'audit ne justifie
    /// pas de perdre un décodage en cours.
    /// </summary>
    public static async Task<ModelRuntimeInfo> FetchAsync(
        LlmOptions options, string modelId, CancellationToken ct)
    {
        if (options.Provider != LlmProvider.OpenAI)
            return ModelRuntimeInfo.Unknown;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var payload = await http
                .GetStringAsync(EndpointFor(options.BaseUrl), ct)
                .ConfigureAwait(false);

            return Parse(payload, modelId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.Error.WriteLine(
                $"AVERTISSEMENT : impossible de lire {EndpointFor(options.BaseUrl)} ({ex.Message}). " +
                "quantization et loadedContextLength seront null.");
            return ModelRuntimeInfo.Unknown;
        }
    }
}
