using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests;

/// <summary>
/// Contrat de /api/config pour le double contrôle de la feature « joueurs IA » :
/// flag appsettings ET disponibilité de la clé API. aiPlayersEnabled reste le booléen
/// *effectif* (le front continue de s'en servir tel quel pour griser le bouton) ;
/// aiPlayersUnavailableReason ne sert qu'à choisir le message de survol.
/// </summary>
public class PublicConfigAiAvailabilityHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicConfigAiAvailabilityHttpTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    private sealed class StubProbe : ILlmApiKeyProbe
    {
        private readonly bool _available;
        public StubProbe(bool available) => _available = available;

        public int CallCount { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_available);
        }
    }

    private (HttpClient client, StubProbe probe) BuildClient(bool flagEnabled, bool apiKeyAvailable)
    {
        var probe = new StubProbe(apiKeyAvailable);
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.Configure<AIPlayersOptions>(o => o.Enabled = flagEnabled);
                services.RemoveAll<ILlmApiKeyProbe>();
                services.AddSingleton<ILlmApiKeyProbe>(probe);
            })).CreateClient();
        return (client, probe);
    }

    private static async Task<(bool enabled, string? reason)> ReadConfig(HttpClient client)
    {
        var response = await client.GetAsync("/api/config");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var enabled = doc.RootElement.GetProperty("aiPlayersEnabled").GetBoolean();
        var reasonElement = doc.RootElement.GetProperty("aiPlayersUnavailableReason");
        var reason = reasonElement.ValueKind == JsonValueKind.Null ? null : reasonElement.GetString();
        return (enabled, reason);
    }

    [Fact]
    public async Task Flag_on_and_api_key_available_enables_the_feature()
    {
        var (client, _) = BuildClient(flagEnabled: true, apiKeyAvailable: true);

        var (enabled, reason) = await ReadConfig(client);

        Assert.True(enabled);
        Assert.Null(reason);
    }

    [Fact]
    public async Task Flag_on_but_api_key_revoked_disables_the_feature()
    {
        var (client, _) = BuildClient(flagEnabled: true, apiKeyAvailable: false);

        var (enabled, reason) = await ReadConfig(client);

        Assert.False(enabled);
        Assert.Equal("apiKeyUnavailable", reason);
    }

    [Fact]
    public async Task Flag_off_short_circuits_without_probing()
    {
        // Le && de C# doit court-circuiter : inutile d'appeler le provider quand
        // l'administrateur a déjà coupé la feature côté configuration.
        var (client, probe) = BuildClient(flagEnabled: false, apiKeyAvailable: true);

        var (enabled, reason) = await ReadConfig(client);

        Assert.False(enabled);
        Assert.Equal("disabled", reason);
        Assert.Equal(0, probe.CallCount);
    }
}
