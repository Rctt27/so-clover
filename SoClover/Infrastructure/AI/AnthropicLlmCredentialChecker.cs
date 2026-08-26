using Microsoft.Extensions.Options;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Sonde la validité de la clé Anthropic via <c>GET /v1/models</c>.
///
/// Cet endpoint est retenu parce qu'il est authentifié par <c>x-api-key</c> mais ne consomme
/// aucun token — il n'est donc pas facturé et peut être appelé aussi souvent que nécessaire.
/// Une clé révoquée depuis la console Anthropic y répond
/// <c>401 {"error":{"type":"authentication_error"}}</c>.
///
/// Appel HTTP direct plutôt que via Anthropic.SDK : le verdict repose sur le code de statut
/// exact (distinguer 401 d'un 5xx), et un HttpClient s'injecte en test avec un handler
/// factice, là où <c>AnthropicClient</c> ne le permet pas.
///
/// Limite connue : cette sonde détecte une clé révoquée ou désactivée, pas un solde de crédits
/// épuisé — ce dernier laisse /v1/models répondre 200 et n'échoue que sur /v1/messages.
/// </summary>
public sealed class AnthropicLlmCredentialChecker : ILlmCredentialChecker
{
    private const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string AnthropicVersion = "2023-06-01";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly IOptions<LlmOptions> _options;

    public AnthropicLlmCredentialChecker(HttpClient http, IOptions<LlmOptions> options)
    {
        _http = http;
        _options = options;
    }

    public async Task<LlmCredentialStatus> CheckAsync(CancellationToken ct = default)
    {
        var apiKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return LlmCredentialStatus.Invalid;

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsUri(_options.Value.BaseUrl));
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);

            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => LlmCredentialStatus.Invalid,
                System.Net.HttpStatusCode.Forbidden => LlmCredentialStatus.Invalid,
                _ when response.IsSuccessStatusCode => LlmCredentialStatus.Valid,
                _ => LlmCredentialStatus.Indeterminate
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Timeout, DNS, TLS, socket… : aucune conclusion sur la clé.
            return LlmCredentialStatus.Indeterminate;
        }
    }

    /// <summary>
    /// Tolère les formes avec ou sans <c>/v1</c> et avec ou sans slash final : la valeur de
    /// <c>Llm:BaseUrl</c> n'est pas utilisée par le chemin Anthropic de ChatClientFactory,
    /// donc rien ne garantit sa forme.
    /// </summary>
    private static Uri BuildModelsUri(string? baseUrl)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        root = root.TrimEnd('/');

        if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            root = root[..^3].TrimEnd('/');

        return new Uri($"{root}/v1/models?limit=1");
    }
}
