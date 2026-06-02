using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using Xunit;

namespace SoClover.Tests;

/// <summary>
/// Bug n°1 : le endpoint HTTP <c>GET /api/games/{id}/state</c> re-mappe <c>guessingState</c> vers un
/// objet anonyme (Program.cs) qui OMET <c>cumulativeBoardRotation</c>. La réponse HTTP renvoie donc
/// une rotation absente — le client la coerce en 0 (<c>?? 0</c>) et, comme la validation a bumpé la
/// révision, l'applique → le plateau « snap » à 0 chez le valideur.
///
/// Contraste : la diffusion SignalR sérialise directement le record typé <c>GetGameState.Response</c>
/// (qui porte bien <c>cumulativeBoardRotation</c>), d'où l'asymétrie HTTP/SignalR observée en jeu.
///
/// On boote l'app via <see cref="WebApplicationFactory{TEntryPoint}"/>. En build DEBUG, le repository
/// est <c>InMemoryGameRepository</c> (singleton) : on sème une partie directement, puis on frappe
/// le endpoint et on inspecte le JSON renvoyé.
/// </summary>
public class GetGameStateHttpRotationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetGameStateHttpRotationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetState_DuringGuessing_HttpResponseExposesCumulativeBoardRotation()
    {
        // Arrange — partie en phase Guessing, plateau tourné de 90°, semée dans le repo singleton.
        var repo = _factory.Services.GetRequiredService<IGameRepository>();
        var (game, _, guesserId) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        game.RotateBoard(90);
        await repo.Save(game);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            $"/api/games/{game.Id.Value}/state?playerId={guesserId.Value}&includeSecrets=false");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // Assert — la réponse HTTP doit exposer guessingState.cumulativeBoardRotation = 90.
        using var doc = JsonDocument.Parse(json);
        var guessingState = doc.RootElement.GetProperty("guessingState");
        Assert.True(
            guessingState.TryGetProperty("cumulativeBoardRotation", out var rotation),
            "La réponse HTTP /state doit exposer guessingState.cumulativeBoardRotation (omis par le mapping anonyme de Program.cs).");
        Assert.Equal(90, rotation.GetInt32());
    }
}
