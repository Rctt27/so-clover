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

public class JudgeServerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"judge-srv-{Guid.NewGuid():N}.jsonl");
    private readonly string _pagePath = Path.Combine(Path.GetTempPath(), $"judge-page-{Guid.NewGuid():N}.html");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);

    public JudgeServerTests() => File.WriteAllText(_pagePath, "<!doctype html><title>séance B</title>");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_pagePath)) File.Delete(_pagePath);
    }

    private JudgeSession NewSession()
    {
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, annotated, assistCount: 12);
        var plan = ComparisonPlan.Build(
            _bench, elicitation,
            HumanTestData.Run(_bench, "run-a", "modelA"), "run-a",
            HumanTestData.Run(_bench, "run-b", "modelB"), "run-b",
            HumanTestData.Run(_bench, "run-rnd", "aleatoire"), "run-rnd",
            seed: 20260730001, targetCount: 30);

        if (!File.Exists(_path))
            HumanFile.WriteComparisonManifest(_path, HumanTestData.ComparisonManifest(
                _bench.Manifest.BenchHash, plan.Count));

        return new JudgeSession(
            _bench, plan, HumanFile.ReadComparisons(_path), _path, "s-http",
            quotaBeforePause: 50, pauseSeconds: 0);
    }

    [Fact]
    public async Task La_reponse_de_next_ne_contient_jamais_la_provenance()
    {
        var app = HumanServer.BuildJudgeApp(NewSession(), _pagePath, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) };
        try
        {
            var payload = await client.GetStringAsync("/api/next");

            // L'aveuglement est structurel : ces champs n'existent pas dans le contrat de sortie.
            Assert.DoesNotContain("source", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("runId", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("family", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("comparisonId", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("duplicateOf", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("optionA", payload, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("position1Clue", payload, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Un_verdict_est_consigne_et_le_re_jugement_ajoute_une_ligne()
    {
        var app = HumanServer.BuildJudgeApp(NewSession(), _pagePath, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) };
        try
        {
            await client.GetAsync("/api/next");

            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/verdict", new { positionChoice = "1", elapsedMs = 9000 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/rejudge", new { positionChoice = "tie", elapsedMs = 3000 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/verdict", new { positionChoice = "9", elapsedMs = 1000 })).StatusCode);

            Assert.Equal(2, HumanFile.ReadComparisons(_path).Comparisons.Count);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task La_page_juge_est_servie_sur_la_racine()
    {
        var app = HumanServer.BuildJudgeApp(NewSession(), _pagePath, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) };
        try
        {
            Assert.Contains("séance B", await client.GetStringAsync("/"), StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
