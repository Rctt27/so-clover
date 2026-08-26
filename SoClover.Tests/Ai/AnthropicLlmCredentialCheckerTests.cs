using System.Net;
using Microsoft.Extensions.Options;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

/// <summary>
/// Mapping statut HTTP → verdict pour la sonde Anthropic. GET /v1/models est retenu parce
/// qu'il est authentifié par x-api-key mais ne consomme aucun token : on peut le sonder
/// aussi souvent qu'on veut sans facturation.
/// </summary>
public class AnthropicLlmCredentialCheckerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(HttpStatusCode status)
            => _respond = _ => new HttpResponseMessage(status);

        public StubHandler(Exception toThrow)
            => _respond = _ => throw toThrow;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private static (AnthropicLlmCredentialChecker checker, StubHandler handler) Build(
        StubHandler handler,
        string baseUrl = "https://api.anthropic.com",
        string apiKey = "sk-ant-test")
    {
        var options = Options.Create(new LlmOptions
        {
            Provider = LlmProvider.Anthropic,
            BaseUrl = baseUrl,
            ApiKey = apiKey
        });
        var checker = new AnthropicLlmCredentialChecker(new HttpClient(handler), options);
        return (checker, handler);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, LlmCredentialStatus.Valid)]
    [InlineData(HttpStatusCode.Unauthorized, LlmCredentialStatus.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, LlmCredentialStatus.Invalid)]
    [InlineData(HttpStatusCode.TooManyRequests, LlmCredentialStatus.Indeterminate)]
    [InlineData(HttpStatusCode.InternalServerError, LlmCredentialStatus.Indeterminate)]
    [InlineData(HttpStatusCode.BadGateway, LlmCredentialStatus.Indeterminate)]
    public async Task Maps_http_status_to_verdict(HttpStatusCode status, LlmCredentialStatus expected)
    {
        var (checker, _) = Build(new StubHandler(status));

        Assert.Equal(expected, await checker.CheckAsync());
    }

    [Fact]
    public async Task Transport_failure_is_indeterminate()
    {
        var (checker, _) = Build(new StubHandler(new HttpRequestException("no route to host")));

        Assert.Equal(LlmCredentialStatus.Indeterminate, await checker.CheckAsync());
    }

    [Fact]
    public async Task Timeout_is_indeterminate()
    {
        var (checker, _) = Build(new StubHandler(new TaskCanceledException("timeout")));

        Assert.Equal(LlmCredentialStatus.Indeterminate, await checker.CheckAsync());
    }

    [Fact]
    public async Task Sends_api_key_and_version_headers()
    {
        var (checker, handler) = Build(new StubHandler(HttpStatusCode.OK));

        await checker.CheckAsync();

        var request = Assert.IsType<HttpRequestMessage>(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("sk-ant-test", Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
    }

    [Theory]
    [InlineData("https://api.anthropic.com")]
    [InlineData("https://api.anthropic.com/")]
    [InlineData("https://api.anthropic.com/v1")]
    [InlineData("https://api.anthropic.com/v1/")]
    [InlineData("")]
    public async Task Resolves_the_models_endpoint_whatever_the_configured_base_url(string baseUrl)
    {
        // BaseUrl est ignoré par ChatClientFactory côté Anthropic (le SDK a son endpoint par
        // défaut) : la sonde doit tolérer les formes avec ou sans /v1 plutôt que de dépendre
        // d'une convention non vérifiée ailleurs.
        var (checker, handler) = Build(new StubHandler(HttpStatusCode.OK), baseUrl);

        await checker.CheckAsync();

        Assert.Equal(
            "https://api.anthropic.com/v1/models?limit=1",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Missing_api_key_is_invalid_without_any_network_call()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var (checker, _) = Build(handler, apiKey: "   ");

        Assert.Equal(LlmCredentialStatus.Invalid, await checker.CheckAsync());
        Assert.Null(handler.LastRequest);
    }
}
