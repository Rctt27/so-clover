using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using SoClover.Eval.Config;
using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class LangfuseClientTests
{
    private const string PromptV2 = """
        {"name":"decoder-fr-clue","type":"text","version":2,"prompt":"---\nversion: 4\n---\n# SYSTEM\nDevine.","labels":["v4","production"]}
        """;

    [Fact]
    public async Task Lit_un_prompt_par_label_avec_une_authentification_basic()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(PromptV2));
        var client = LangfuseStubHandler.Client(handler);

        var prompt = await client.GetPromptAsync("decoder-fr-clue", "production", null, CancellationToken.None);

        Assert.NotNull(prompt);
        Assert.Equal(2, prompt!.Version);
        Assert.StartsWith("---\nversion: 4", prompt.Content);
        Assert.Contains("production", prompt.Labels);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/public/v2/prompts/decoder-fr-clue?label=production", request.PathAndQuery);
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("pk-lf-test:sk-lf-test"));
        Assert.Equal($"Basic {expected}", request.Authorization);
    }

    [Fact]
    public async Task Une_version_explicite_prime_sur_le_label()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(PromptV2));

        await LangfuseStubHandler.Client(handler)
            .GetPromptAsync("decoder-fr-clue", "production", 2, CancellationToken.None);

        Assert.Equal("/api/public/v2/prompts/decoder-fr-clue?version=2", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task Un_prompt_absent_rend_null_et_non_une_exception()
    {
        var handler = new LangfuseStubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        var prompt = await LangfuseStubHandler.Client(handler)
            .GetPromptAsync("inconnu", "production", null, CancellationToken.None);

        Assert.Null(prompt);
    }

    [Fact]
    public async Task L_historique_relit_chaque_version_listee()
    {
        var handler = new LangfuseStubHandler((request, _) =>
            request.RequestUri!.AbsolutePath == "/api/public/v2/prompts"
                ? LangfuseStubHandler.Json("""{"data":[{"name":"decoder-fr-clue","versions":[1,2]}],"meta":{}}""")
                : LangfuseStubHandler.Json(PromptV2.Replace("\"version\":2",
                    request.RequestUri.Query.EndsWith("=1") ? "\"version\":1" : "\"version\":2")));

        var history = await LangfuseStubHandler.Client(handler)
            .GetPromptHistoryAsync("decoder-fr-clue", CancellationToken.None);

        Assert.Equal(new[] { 1, 2 }, history.Select(p => p.Version));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Creer_un_prompt_envoie_type_text_labels_et_version_soclover()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(PromptV2));

        await LangfuseStubHandler.Client(handler).CreatePromptAsync(
            "decoder-fr-clue", "contenu", ["v4", "production"], 4, "sync git", CancellationToken.None);

        var body = JsonNode.Parse(handler.Requests[0].Body!)!.AsObject();
        Assert.Equal("text", (string?)body["type"]);
        Assert.Equal("contenu", (string?)body["prompt"]);
        Assert.Equal(new[] { "v4", "production" }, body["labels"]!.AsArray().Select(l => (string?)l));
        Assert.Equal(4, (int)body["config"]!["socloverVersion"]!);
        Assert.Equal("sync git", (string?)body["commitMessage"]);
    }

    [Fact]
    public async Task Une_erreur_http_leve_une_LangfuseException_avec_le_statut()
    {
        var handler = new LangfuseStubHandler((_, _) =>
            LangfuseStubHandler.Json("""{"message":"Invalid credentials"}""", HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<LangfuseException>(() => LangfuseStubHandler.Client(handler)
            .GetPromptAsync("decoder-fr-clue", "production", null, CancellationToken.None));

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Un_serveur_injoignable_leve_une_LangfuseException_qui_nomme_le_repli()
    {
        var handler = new LangfuseStubHandler((_, _) => throw new HttpRequestException("connexion refusée"));

        var ex = await Assert.ThrowsAsync<LangfuseException>(() => LangfuseStubHandler.Client(handler)
            .GetPromptAsync("decoder-fr-clue", "production", null, CancellationToken.None));

        Assert.Contains("--prompt-source file", ex.Message);
    }

    [Fact]
    public void La_section_Langfuse_se_lie_et_ses_defauts_valent_source_langfuse_label_production()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var defaults = EvalLlmConfig.BindLangfuse(empty);
        Assert.Equal(PromptSource.Langfuse, defaults.PromptSource);
        Assert.Equal("production", defaults.PromptLabel);
        Assert.False(defaults.HasCredentials);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Langfuse:baseUrl"] = "http://localhost:3000",
            ["Langfuse:publicKey"] = "pk",
            ["Langfuse:secretKey"] = "sk",
            ["Langfuse:promptSource"] = "File",
        }).Build();
        var bound = EvalLlmConfig.BindLangfuse(config);
        Assert.Equal(PromptSource.File, bound.PromptSource);
        Assert.True(bound.HasCredentials);
    }

    [Fact]
    public void Sans_cles_la_fabrique_rend_null_et_la_variante_requise_explique_quoi_renseigner()
    {
        var options = new LangfuseOptions();

        Assert.Null(LangfuseClientFactory.CreateOrNull(options));
        var ex = Assert.Throws<InvalidOperationException>(() => LangfuseClientFactory.CreateRequired(options));
        Assert.Contains("LANGFUSE__PUBLICKEY", ex.Message);
    }

    [Fact]
    public async Task Un_item_de_dataset_est_poste_avec_son_id_et_le_nom_du_dataset()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("{}"));
        var item = BenchDatasetMapper.ToItems(LangfuseFixtures.Bench())[0];

        await LangfuseStubHandler.Client(handler).UpsertDatasetItemAsync("soclover-bench-dev", item, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/public/dataset-items", request.PathAndQuery);
        var body = JsonNode.Parse(request.Body!)!.AsObject();
        Assert.Equal("soclover-bench-dev", (string?)body["datasetName"]);
        Assert.Equal("dev-001-Top", (string?)body["id"]);
        Assert.NotNull(body["expectedOutput"]);
    }

    [Fact]
    public async Task Un_dataset_se_relit_avec_son_benchHash()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(
            """{"id":"ds_1","name":"soclover-bench-dev","metadata":{"benchHash":"416b819a41a1"}}"""));

        var dataset = await LangfuseStubHandler.Client(handler).GetDatasetAsync("soclover-bench-dev", CancellationToken.None);

        Assert.Equal(new LangfuseDataset("ds_1", "soclover-bench-dev", "416b819a41a1"), dataset);
        Assert.Equal("/api/public/v2/datasets/soclover-bench-dev", handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task Un_score_est_poste_en_NUMERIC_sans_champs_nuls()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("{}"));

        await LangfuseStubHandler.Client(handler).CreateScoreAsync(
            new LangfuseScore("id1", "r", 0.5, "trace1", "span1", null, null), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/public/scores", request.PathAndQuery);
        var body = JsonNode.Parse(request.Body!)!.AsObject();
        Assert.Equal("NUMERIC", (string?)body["dataType"]);
        Assert.Equal("span1", (string?)body["observationId"]);
        Assert.False(body.ContainsKey("datasetRunId"));
        Assert.False(body.ContainsKey("comment"));
    }

    [Fact]
    public async Task Les_traces_OTLP_partent_avec_l_en_tete_d_ingestion_v4()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("{}"));

        await LangfuseStubHandler.Client(handler).SendOtlpTracesAsync(new JsonObject { ["resourceSpans"] = new JsonArray() }, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/public/otel/v1/traces", request.PathAndQuery);
        Assert.Equal("4", request.Headers["x-langfuse-ingestion-version"]);
    }

    /// <summary>
    /// L'instance locale tourne en mode « events_only » : `GET /api/public/datasets/{nom}/runs/{run}`
    /// n'existe pas (spike du 2026-09-16, §11 de la spec). L'id d'experiment se lit dans la liste
    /// `/api/public/experiments`, filtrée sur une fenêtre de temps puis sur le nom.
    /// </summary>
    [Fact]
    public async Task L_id_d_experiment_se_lit_dans_la_liste_filtree_par_fenetre_puis_par_nom()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(
            """{"data":[{"id":"exp_1","name":"run.9a829dc206d2"},{"id":"exp_2","name":"un-autre"}]}"""));

        var id = await LangfuseStubHandler.Client(handler).FindExperimentIdAsync(
            "run.9a829dc206d2",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal("exp_1", id);
        Assert.StartsWith("/api/public/experiments?fromStartTime=2026-07-27T00%3A00%3A00Z&toStartTime=2026-09-17T00%3A00%3A00Z",
            handler.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task Un_experiment_absent_de_la_liste_rend_null()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("""{"data":[]}"""));

        Assert.Null(await LangfuseStubHandler.Client(handler).FindExperimentIdAsync(
            "run.9a829dc206d2", DateTime.UnixEpoch, DateTime.UnixEpoch, CancellationToken.None));
    }
}
