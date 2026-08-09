using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Web;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ElicitServerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"elicit-srv-{Guid.NewGuid():N}.jsonl");
    private readonly string _pagePath = Path.Combine(Path.GetTempPath(), $"page-{Guid.NewGuid():N}.html");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 10);

    public ElicitServerTests() => File.WriteAllText(_pagePath, "<!doctype html><title>séance A</title>");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_pagePath)) File.Delete(_pagePath);
    }

    // pauseSeconds par défaut à 0 : la seule horloge disponible ici est l'horloge système réelle
    // (aucune ElicitationSession construite dans ce fichier ne passe de `clock:`), et
    // Next()/IsPauseDue calcule `remaining = pauseSeconds - elapsedInt` où elapsedInt est la
    // troncature entière d'un écart de quelques microsecondes entre deux appels consécutifs à
    // l'horloge — donc toujours 0. Avec pauseSeconds: 0, `remaining > 0` n'est donc jamais vrai et
    // la pause s'auto-acquitte instantanément : c'est correct pour les 5 autres tests HTTP (qui ne
    // veulent PAS être ralentis par une vraie pause), mais rendrait le test de pause
    // ci-dessous faux à coup sûr — d'où le paramètre explicite pour ce seul test.
    private ElicitationSession NewSession(int targetCount = 6, int quota = 25, int pauseSeconds = 0)
    {
        if (!File.Exists(_path))
            HumanFile.WriteElicitationManifest(_path, HumanTestData.ElicitationManifest(
                _bench.Manifest.BenchHash, targetCount, quota));

        return new ElicitationSession(
            _bench,
            ElicitationPlan.Build(_bench, seed: 42, targetCount),
            HumanFile.ReadElicitation(_path),
            ElicitationSession.BuildCandidateIndex(null),
            new FrenchOffClueValidator(),
            _path, "s-http", timerSeconds: 90, quotaBeforePause: quota, pauseSeconds: pauseSeconds);
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        ElicitationSession session, string pagePath)
    {
        var app = HumanServer.BuildElicitApp(session, pagePath, port: 0);
        await app.StartAsync();
        return (app, new HttpClient { BaseAddress = new Uri(HumanServer.ResolveUrl(app)) });
    }

    private static async Task StopAsync(WebApplication app, HttpClient client)
    {
        client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task La_page_est_servie_sur_la_racine()
    {
        var (app, client) = await StartAsync(NewSession(), _pagePath);
        try
        {
            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("séance A", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally { await StopAsync(app, client); }
    }

    [Fact]
    public async Task Les_candidats_repondent_409_avant_tentative_et_200_apres()
    {
        var (app, client) = await StartAsync(NewSession(), _pagePath);
        try
        {
            await client.GetAsync("/api/next");

            Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/candidates")).StatusCode);

            var attempt = await client.PostAsJsonAsync("/api/attempt", new
            {
                clue = "Pédiatre", outcome = "solide", relationType = "R12_specialisation_croisee", elapsedSeconds = 42,
            });
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/candidates")).StatusCode);
        }
        finally { await StopAsync(app, client); }
    }

    [Fact]
    public async Task Un_indice_illegal_repond_422_avec_sa_regle()
    {
        var session = NewSession();
        var (app, client) = await StartAsync(session, _pagePath);
        try
        {
            var next = await client.GetFromJsonAsync<JsonElement>("/api/next");
            var boardId = next.GetProperty("boardId").GetString();
            var boardWord = _bench.Boards.Single(b => b.BoardId == boardId).Cards[0][0];

            var response = await client.PostAsJsonAsync("/api/attempt", new
            {
                clue = boardWord, outcome = "solide", relationType = (string?)null, elapsedSeconds = 5,
            });

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("ExactMatch", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally { await StopAsync(app, client); }
    }

    [Fact]
    public async Task Un_relationType_inconnu_repond_400()
    {
        var (app, client) = await StartAsync(NewSession(), _pagePath);
        try
        {
            await client.GetAsync("/api/next");

            var response = await client.PostAsJsonAsync("/api/attempt", new
            {
                clue = "Pédiatre", outcome = "solide", relationType = "R99_inventee", elapsedSeconds = 5,
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally { await StopAsync(app, client); }
    }

    [Fact]
    public async Task La_seance_reprend_apres_un_redemarrage_du_serveur()
    {
        var (app, client) = await StartAsync(NewSession(), _pagePath);
        try
        {
            await client.GetAsync("/api/next");
            await client.PostAsJsonAsync("/api/attempt", new
            {
                clue = "Pédiatre", outcome = "solide", relationType = (string?)null, elapsedSeconds = 10,
            });
            await client.PostAsJsonAsync("/api/assisted", new { assistedClue = (string?)null, notes = (string?)null });
        }
        finally { await StopAsync(app, client); }

        var (app2, client2) = await StartAsync(NewSession(), _pagePath);
        try
        {
            var next = await client2.GetFromJsonAsync<JsonElement>("/api/next");

            Assert.Equal(1, next.GetProperty("completedCount").GetInt32());
            Assert.False(next.GetProperty("awaitingReveal").GetBoolean());
        }
        finally { await StopAsync(app2, client2); }
    }

    [Fact]
    public async Task Lecran_de_pause_apparait_au_dela_du_quota()
    {
        var (app, client) = await StartAsync(
            NewSession(targetCount: 6, quota: 2, pauseSeconds: 300), _pagePath);
        try
        {
            for (var i = 0; i < 2; i++)
            {
                await client.GetAsync("/api/next");
                await client.PostAsJsonAsync("/api/attempt", new
                {
                    clue = i == 0 ? "Pédiatre" : "Radiologue",
                    outcome = "solide", relationType = (string?)null, elapsedSeconds = 10,
                });
                await client.PostAsJsonAsync("/api/assisted", new { assistedClue = (string?)null, notes = (string?)null });
            }

            var paused = await client.GetFromJsonAsync<JsonElement>("/api/next");

            Assert.True(paused.GetProperty("pauseRequired").GetBoolean());
        }
        finally { await StopAsync(app, client); }
    }

    // ── Trou de couverture comblé : HumanFile.RequireBench ───────────────────────
    //
    // RequireBench refuse de reprendre une séance sur un banc qui a bougé — sans quoi la reprise
    // produirait des paires cibles fausses sans le signaler. Ce garde-fou n'était consommé par
    // aucun test depuis T3 (le fichier de RequireBench n'est appelé que dans le branchement
    // « reprise » d'EvalProgram.Elicit, qui vit dans cette tâche) : ce test le traverse directement
    // avec un banc dont le hash diverge du manifeste, exactement le scénario qu'il doit bloquer.
    [Fact]
    public void La_reprise_est_refusee_si_le_banc_a_bouge()
    {
        HumanFile.WriteElicitationManifest(_path, HumanTestData.ElicitationManifest(
            benchHash: "hash-original-du-manifeste", targetCount: 6, quotaBeforePause: 25));
        var existing = HumanFile.ReadElicitation(_path);

        var benchQuiABouge = HumanTestData.Bench(boardCount: 10, benchHash: "hash-different-relu");

        var ex = Assert.Throws<HumanIntegrityException>(() =>
            HumanFile.RequireBench(_path, existing.Manifest.BenchHash, benchQuiABouge));

        Assert.Contains("hash-original-du-manifeste", ex.Message, StringComparison.Ordinal);
        Assert.Contains("hash-different-relu", ex.Message, StringComparison.Ordinal);
    }

    // ── Preuve que la page vient de la sortie de build, pas des sources ──────────
    //
    // Les six tests ci-dessus passent délibérément un fichier temporaire à BuildElicitApp — ils
    // exercent le protocole HTTP, pas l'empaquetage. Ce test-ci exerce l'autre moitié du Step 6 du
    // brief : que <Content Include="Web\Pages\**\*.html"> du csproj copie réellement elicit.html
    // vers AppContext.BaseDirectory (celui de SoClover.Tests, par propagation transitive), avec le
    // MÊME calcul de chemin que EvalProgram.Elicit (Program.cs). Une régression du csproj (item mal
    // filtré, CopyToOutputDirectory oublié) ferait échouer File.Exists ici sans jamais toucher aux
    // six tests précédents.
    [Fact]
    public async Task La_page_elicit_est_copiee_par_le_csproj_et_servie_depuis_la_sortie_de_build()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "elicit.html");

        Assert.True(
            outputPath.Contains(Path.Combine("bin", "Debug"), StringComparison.OrdinalIgnoreCase) ||
            outputPath.Contains(Path.Combine("bin", "Release"), StringComparison.OrdinalIgnoreCase),
            $"AppContext.BaseDirectory ({AppContext.BaseDirectory}) ne ressemble pas à une sortie " +
            "de build : ce test doit lire une copie, pas l'arborescence source.");
        Assert.True(File.Exists(outputPath),
            $"Page absente de la sortie de build : {outputPath}. Le csproj ne l'embarque pas " +
            "(vérifier l'item Content Web\\Pages\\**\\*.html et sa propagation vers SoClover.Tests).");

        var (app, client) = await StartAsync(NewSession(), outputPath);
        try
        {
            var response = await client.GetAsync("/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Séance A", html, StringComparison.Ordinal);
        }
        finally { await StopAsync(app, client); }
    }
}
