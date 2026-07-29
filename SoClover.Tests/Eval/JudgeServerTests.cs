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

            // Défaut Critique corrigé en revue : sans repasser par /api/next, une soumission
            // directe pour le couple suivant est refusée (409), pas silencieusement acceptée —
            // sans quoi la page serait le seul rempart contre un item jamais servi.
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/api/verdict", new { positionChoice = "2", elapsedMs = 500 })).StatusCode);

            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/rejudge", new { positionChoice = "tie", elapsedMs = 3000 })).StatusCode);

            await client.GetAsync("/api/next");
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

    // ── Trou de couverture comblé (revue, IMPORTANT 3) ───────────────────────────
    //
    // Le test ci-dessus passe délibérément un fichier temporaire à BuildJudgeApp — il exerce le
    // routage HTTP, pas l'empaquetage. Ce test-ci exerce l'autre moitié : que
    // <Content Include="Web\Pages\**\*.html"> du csproj copie réellement judge.html vers
    // AppContext.BaseDirectory (celui de SoClover.Tests, par propagation transitive), avec le MÊME
    // calcul de chemin que EvalProgram.Judge (Program.cs). Même patron que
    // ElicitServerTests.La_page_elicit_est_copiee_par_le_csproj_et_servie_depuis_la_sortie_de_build
    // (T6). Une régression du csproj (item mal filtré, CopyToOutputDirectory oublié) ferait
    // échouer File.Exists ici sans jamais toucher aux tests précédents.
    [Fact]
    public async Task La_page_judge_est_copiee_par_le_csproj_et_servie_depuis_la_sortie_de_build()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "judge.html");

        Assert.True(
            outputPath.Contains(Path.Combine("bin", "Debug"), StringComparison.OrdinalIgnoreCase) ||
            outputPath.Contains(Path.Combine("bin", "Release"), StringComparison.OrdinalIgnoreCase),
            $"AppContext.BaseDirectory ({AppContext.BaseDirectory}) ne ressemble pas à une sortie " +
            "de build : ce test doit lire une copie, pas l'arborescence source.");
        Assert.True(File.Exists(outputPath),
            $"Page absente de la sortie de build : {outputPath}. Le csproj ne l'embarque pas " +
            "(vérifier l'item Content Web\\Pages\\**\\*.html et sa propagation vers SoClover.Tests).");

        var app = HumanServer.BuildJudgeApp(NewSession(), outputPath, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) };
        try
        {
            var response = await client.GetAsync("/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Séance B", html, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
