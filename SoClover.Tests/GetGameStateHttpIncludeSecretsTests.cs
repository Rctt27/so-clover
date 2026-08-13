using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using Xunit;

namespace SoClover.Tests;

/// <summary>
/// Le paramètre de query <c>includeSecrets</c> de <c>GET /api/games/{id}/state</c> était déclaré
/// <c>bool</c> non nullable : ASP.NET Core le considérait alors comme **requis** et rejetait tout
/// appel sans <c>?includeSecrets=...</c> par un 400 au binding (corps vide). Le client passe
/// toujours le paramètre, mais tout appel manuel / outillage tombait dans le piège.
///
/// Le paramètre doit être optionnel, avec le défaut **sûr** <c>false</c> (ne pas divulguer les
/// secrets quand il est absent), sans changer la sémantique quand il est fourni.
/// </summary>
public class GetGameStateHttpIncludeSecretsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetGameStateHttpIncludeSecretsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetState_SansParametreIncludeSecrets_Renvoie200AvecSecretsMasques()
    {
        // Arrange — partie semée dans le repository singleton (InMemory en DEBUG).
        var repo = _factory.Services.GetRequiredService<IGameRepository>();
        var (game, _, _) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        await repo.Save(game);

        var client = _factory.CreateClient();

        // Act — aucun `includeSecrets` dans la query string.
        var response = await client.GetAsync($"/api/games/{game.Id.Value}/state");

        // Assert — le endpoint répond, et se comporte comme `includeSecrets=false`.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var boards = doc.RootElement.GetProperty("players").EnumerateArray()
            .Select(p => p.GetProperty("board"))
            .ToList();

        Assert.NotEmpty(boards);
        foreach (var board in boards)
        {
            foreach (var direction in new[] { "top", "right", "bottom", "left" })
            {
                Assert.Equal(
                    JsonValueKind.Null,
                    board.GetProperty(direction).GetProperty("card").ValueKind);
            }
        }
    }

    [Fact]
    public async Task GetState_AvecIncludeSecretsTrue_ExposeToujoursLesCartes()
    {
        // Arrange
        var repo = _factory.Services.GetRequiredService<IGameRepository>();
        var (game, ownerId, _) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        await repo.Save(game);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/games/{game.Id.Value}/state?includeSecrets=true");

        // Assert — sémantique inchangée quand le paramètre est explicitement fourni.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ownerBoard = doc.RootElement.GetProperty("players").EnumerateArray()
            .Single(p => p.GetProperty("playerId").GetString() == ownerId.Value.ToString())
            .GetProperty("board");

        Assert.Equal(
            JsonValueKind.Object,
            ownerBoard.GetProperty("top").GetProperty("card").ValueKind);
    }
}
