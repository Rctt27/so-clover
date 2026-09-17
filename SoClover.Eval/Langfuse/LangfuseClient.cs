using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Client REST minimal de Langfuse. Pas de SDK .NET officiel : appel HTTP direct, sur le modèle
/// d'<c>AnthropicLlmCredentialChecker</c> — le verdict repose sur le code de statut et un
/// <see cref="HttpClient"/> s'injecte en test avec un handler factice.
/// </summary>
public sealed class LangfuseClient
{
    private readonly HttpClient _http;
    private readonly LangfuseOptions _options;

    public LangfuseClient(HttpClient http, LangfuseOptions options)
    {
        if (!options.HasCredentials)
            throw new ArgumentException(
                "LangfuseClient exige LANGFUSE__PUBLICKEY et LANGFUSE__SECRETKEY.", nameof(options));
        _http = http;
        _options = options;
    }

    /// <summary><paramref name="version"/> prime sur <paramref name="label"/> ; sans l'un ni l'autre, <c>production</c>.</summary>
    public async Task<LangfusePrompt?> GetPromptAsync(string name, string? label, int? version, CancellationToken ct)
    {
        var query = version is { } v
            ? $"version={v}"
            : $"label={Uri.EscapeDataString(label ?? "production")}";
        var json = await SendAsync(
            HttpMethod.Get, $"/api/public/v2/prompts/{Uri.EscapeDataString(name)}?{query}", null, ct,
            allowNotFound: true).ConfigureAwait(false);
        return json is null ? null : ParsePrompt(json);
    }

    public async Task<IReadOnlyList<int>> ListPromptVersionsAsync(string name, CancellationToken ct)
    {
        var json = await SendAsync(
            HttpMethod.Get, $"/api/public/v2/prompts?name={Uri.EscapeDataString(name)}", null, ct,
            allowNotFound: true).ConfigureAwait(false);
        if (json?["data"] is not JsonArray data)
            return [];

        return data.OfType<JsonObject>()
            .Where(p => (string?)p["name"] == name)
            .SelectMany(p => p["versions"] is JsonArray versions
                ? versions.Select(x => (int)x!)
                : [])
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>Toutes les versions avec leur contenu : ce que la garde 0 (spec §6.3) doit inspecter.</summary>
    public async Task<IReadOnlyList<LangfusePrompt>> GetPromptHistoryAsync(string name, CancellationToken ct)
    {
        var history = new List<LangfusePrompt>();
        foreach (var version in await ListPromptVersionsAsync(name, ct).ConfigureAwait(false))
        {
            history.Add(await GetPromptAsync(name, null, version, ct).ConfigureAwait(false)
                ?? throw new LangfuseException($"{name} #{version} est listée mais introuvable."));
        }
        return history;
    }

    public async Task<LangfusePrompt> CreatePromptAsync(
        string name, string content, IReadOnlyList<string> labels, int socloverVersion,
        string commitMessage, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["name"] = name,
            ["type"] = "text",
            ["prompt"] = content,
            ["labels"] = new JsonArray(labels.Select(l => (JsonNode?)l).ToArray()),
            ["config"] = new JsonObject { ["socloverVersion"] = socloverVersion },
            ["commitMessage"] = commitMessage,
        };
        var json = await SendAsync(HttpMethod.Post, "/api/public/v2/prompts", body, ct).ConfigureAwait(false)
                   ?? throw new LangfuseException($"Création de {name} : réponse vide.");
        return ParsePrompt(json);
    }

    public async Task<LangfuseDataset?> GetDatasetAsync(string name, CancellationToken ct)
    {
        var json = await SendAsync(
            HttpMethod.Get, $"/api/public/v2/datasets/{Uri.EscapeDataString(name)}", null, ct,
            allowNotFound: true).ConfigureAwait(false);
        return json is null ? null : ParseDataset(json);
    }

    public async Task<LangfuseDataset> CreateDatasetAsync(
        string name, string description, JsonObject metadata, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["metadata"] = metadata.DeepClone(),
        };
        var json = await SendAsync(HttpMethod.Post, "/api/public/v2/datasets", body, ct).ConfigureAwait(false)
                   ?? throw new LangfuseException($"Création du dataset {name} : réponse vide.");
        return ParseDataset(json);
    }

    /// <summary>L'id est fourni : re-poster le même item le met à jour au lieu de le dupliquer (hypothèse H2).</summary>
    public async Task UpsertDatasetItemAsync(string datasetName, LangfuseDatasetItem item, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["datasetName"] = datasetName,
            ["id"] = item.Id,
            ["input"] = item.Input.DeepClone(),
            ["expectedOutput"] = item.ExpectedOutput.DeepClone(),
            ["metadata"] = item.Metadata.DeepClone(),
        };
        await SendAsync(HttpMethod.Post, "/api/public/dataset-items", body, ct).ConfigureAwait(false);
    }

    /// <summary>L'id fourni sert de clé d'idempotence : re-poster met le score à jour.</summary>
    public async Task CreateScoreAsync(LangfuseScore score, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["id"] = score.Id,
            ["name"] = score.Name,
            ["value"] = score.Value,
            ["dataType"] = "NUMERIC",
        };
        if (score.TraceId is not null) body["traceId"] = score.TraceId;
        if (score.ObservationId is not null) body["observationId"] = score.ObservationId;
        if (score.DatasetRunId is not null) body["datasetRunId"] = score.DatasetRunId;
        if (score.Comment is not null) body["comment"] = score.Comment;

        await SendAsync(HttpMethod.Post, "/api/public/scores", body, ct).ConfigureAwait(false);
    }

    /// <summary>OTLP/HTTP JSON. Sans l'en-tête d'ingestion v4, les spans peuvent mettre dix minutes à apparaître.</summary>
    public async Task SendOtlpTracesAsync(JsonObject payload, CancellationToken ct) =>
        await SendAsync(HttpMethod.Post, "/api/public/otel/v1/traces", payload, ct,
            headers: new Dictionary<string, string> { ["x-langfuse-ingestion-version"] = "4" }).ConfigureAwait(false);

    /// <summary>
    /// Id de l'experiment, lu dans la liste filtrée par fenêtre de temps puis par nom.
    /// <para>
    /// `GET /api/public/datasets/{nom}/runs/{run}` n'existe pas sur l'instance mesurée (mode
    /// « events_only », spike du 2026-09-16, §11 de la spec) : c'est `/api/public/experiments` qui
    /// porte la liste. La fenêtre doit couvrir les horodatages **reconstruits** des spans, qui sont
    /// dans le passé — d'où un `fromStartTime` dérivé de la date de création du run, pas de maintenant.
    /// </para>
    /// </summary>
    public async Task<string?> FindExperimentIdAsync(
        string experimentName, DateTime fromStartTime, DateTime toStartTime, CancellationToken ct)
    {
        static string Iso(DateTime moment) =>
            Uri.EscapeDataString(moment.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

        var json = await SendAsync(
            HttpMethod.Get,
            $"/api/public/experiments?fromStartTime={Iso(fromStartTime)}&toStartTime={Iso(toStartTime)}",
            null, ct, allowNotFound: true).ConfigureAwait(false);

        if (json?["data"] is not JsonArray data)
            return null;

        return data.OfType<JsonObject>()
            .Where(e => (string?)e["name"] == experimentName)
            .Select(e => (string?)e["id"])
            .FirstOrDefault();
    }

    private static LangfuseDataset ParseDataset(JsonObject json) => new(
        (string)json["id"]!,
        (string)json["name"]!,
        json["metadata"] is JsonObject metadata ? (string?)metadata["benchHash"] : null);

    internal async Task<JsonObject?> SendAsync(
        HttpMethod method, string pathAndQuery, JsonNode? body, CancellationToken ct,
        bool allowNotFound = false, IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, _options.BaseUrl.TrimEnd('/') + pathAndQuery);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.PublicKey}:{_options.SecretKey}")));
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
                request.Headers.TryAddWithoutValidation(key, value);
        }
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException)
                                   && !ct.IsCancellationRequested)
        {
            throw new LangfuseException(
                $"Langfuse injoignable ({_options.BaseUrl}) : {ex.Message}. Démarrer tools/langfuse " +
                "(docker compose up -d), ou passer --prompt-source file pour travailler hors ligne.", ex);
        }

        using (response)
        {
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return null;

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new LangfuseException(
                    $"Langfuse {method} {pathAndQuery} → {(int)response.StatusCode} : " +
                    (text.Length > 300 ? text[..300] + "…" : text));

            return string.IsNullOrWhiteSpace(text)
                ? new JsonObject()
                : JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        }
    }

    private static LangfusePrompt ParsePrompt(JsonObject json) => new(
        (string)json["name"]!,
        (int)json["version"]!,
        (string)json["prompt"]!,
        json["labels"] is JsonArray labels ? labels.Select(l => (string)l!).ToList() : []);
}
