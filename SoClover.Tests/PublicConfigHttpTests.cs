using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class PublicConfigHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicConfigHttpTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetConfig_exposes_clueMaxLength()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/config");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        Assert.True(
            doc.RootElement.TryGetProperty("clueMaxLength", out var max),
            "/api/config doit exposer clueMaxLength.");
        Assert.Equal(Game.MaxClueLength, max.GetInt32());
    }
}
