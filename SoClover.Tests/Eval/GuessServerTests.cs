using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Web;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class GuessServerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"guess-srv-{Guid.NewGuid():N}.jsonl");
    private readonly string _pagePath = Path.Combine(Path.GetTempPath(), $"guess-page-{Guid.NewGuid():N}.html");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 6);

    public GuessServerTests() => File.WriteAllText(_pagePath, "<!doctype html><title>séance D</title>");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_pagePath)) File.Delete(_pagePath);
        GC.SuppressFinalize(this);
    }

    private GuessingSession NewSession()
    {
        var plan = GuessingPlan.Build(
            _bench, HumanTestData.Run(_bench, "run-a", "gemma"), new HashSet<string>(), seed: 20260807001);

        if (!File.Exists(_path))
            HumanFile.WriteGuessingManifest(_path, new GuessingManifest(
                "manifest", "eval/boards.dev.jsonl", _bench.Manifest.BenchHash, 20260807001,
                "run-a", "eval/runs/run-a.jsonl", null, [], plan.Count, 1, DateTime.UtcNow));

        return new GuessingSession(_bench, plan, HumanFile.ReadGuessing(_path), _path, "s-http");
    }

    private async Task WithClient(Func<HttpClient, Task> body)
    {
        var app = HumanServer.BuildGuessApp(NewSession(), _pagePath, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) };
        try
        {
            await body(client);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    /// <summary>
    /// L'aveuglement doit tenir <b>sur le fil</b>, pas seulement dans le type C# : un onglet ouvert
    /// sur /api/next ne doit rien révéler de la réponse attendue.
    /// </summary>
    [Fact]
    public async Task La_reponse_de_next_ne_contient_ni_reference_ni_board()
    {
        await WithClient(async client =>
        {
            var payload = await client.GetStringAsync("/api/next");
            using var json = JsonDocument.Parse(payload);

            var champs = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.DoesNotContain("referenceWords", champs, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("boardId", champs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("presentedWords", champs, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Un_mot_hors_plateau_rend_400()
    {
        await WithClient(async client =>
        {
            var view = JsonDocument.Parse(await client.GetStringAsync("/api/next"));
            var premier = view.RootElement.GetProperty("presentedWords")[0].GetString();

            var response = await client.PostAsJsonAsync(
                "/api/guess", new { picked = new[] { premier, "mot-inexistant" }, elapsedMs = 500 });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    [Fact]
    public async Task Une_reponse_valide_est_consignee()
    {
        await WithClient(async client =>
        {
            var view = JsonDocument.Parse(await client.GetStringAsync("/api/next"));
            var mots = view.RootElement.GetProperty("presentedWords");

            var response = await client.PostAsJsonAsync("/api/guess", new
            {
                picked = new[] { mots[0].GetString(), mots[1].GetString() },
                elapsedMs = 1200,
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(HumanFile.ReadGuessing(_path).Guesses);
        });
    }

    /// <summary>Aucune API de saut, comme en séances A et B.</summary>
    [Fact]
    public async Task Le_serveur_nexpose_aucune_route_de_saut()
    {
        await WithClient(async client =>
        {
            foreach (var route in new[] { "/api/skip", "/api/next?skip=1", "/api/reveal" })
            {
                var response = await client.PostAsync(route, content: null);
                Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            }
        });
    }
}
