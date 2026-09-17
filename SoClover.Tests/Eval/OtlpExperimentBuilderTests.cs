using System.Text.Json.Nodes;
using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class OtlpExperimentBuilderTests
{
    private const string ExperimentId = "20260728-v5-google-gemma-4-12b-qat-d79a63b9.9a829dc206d2";

    private static OtlpExport Build() => OtlpExperimentBuilder.Build(new ExperimentContext(
        ExperimentId, "ds_1", "9a829dc206d2",
        LangfuseFixtures.Bench(), LangfuseFixtures.Run(), LangfuseFixtures.Decoded()));

    private static IReadOnlyList<JsonObject> Spans(OtlpExport export) => export.Payloads
        .SelectMany(p => p["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]!.AsArray())
        .Select(s => s!.AsObject())
        .ToList();

    private static string? Attr(JsonObject span, string key) => span["attributes"]!.AsArray()
        .Select(a => a!.AsObject())
        .Where(a => (string?)a["key"] == key)
        .Select(a => a["value"]!["stringValue"] is { } s ? (string?)s : a["value"]!["intValue"]?.ToString())
        .SingleOrDefault();

    [Fact]
    public void Une_trace_racine_par_direction_sans_parent_qui_se_designe_elle_meme()
    {
        var roots = Spans(Build()).Where(s => s["parentSpanId"] is null).ToList();

        Assert.Equal(4, roots.Count);
        Assert.All(roots, root =>
        {
            Assert.Equal("experiment-item", (string?)root["name"]);
            Assert.Equal((string?)root["spanId"], Attr(root, "langfuse.experiment.item.root_observation_id"));
        });
        Assert.Equal(
            new[] { "dev-001-Top", "dev-001-Right", "dev-001-Bottom", "dev-001-Left" },
            roots.Select(r => Attr(r, "langfuse.experiment.item.id")));
    }

    [Fact]
    public void Chaque_span_porte_l_identite_de_l_experiment_et_de_son_item()
    {
        foreach (var span in Spans(Build()))
        {
            Assert.Equal(ExperimentId, Attr(span, "langfuse.experiment.id"));
            Assert.Equal(ExperimentId, Attr(span, "langfuse.experiment.name"));
            Assert.Equal("ds_1", Attr(span, "langfuse.experiment.dataset.id"));
            Assert.Equal("9a829dc206d2", Attr(span, "langfuse.experiment.metadata.decoder_fingerprint"));
            Assert.NotNull(Attr(span, "langfuse.experiment.item.id"));
            Assert.NotNull(Attr(span, "langfuse.experiment.item.root_observation_id"));
        }
    }

    [Fact]
    public void Les_tentatives_et_les_decodages_sont_enfants_de_la_racine_de_leur_item()
    {
        var export = Build();
        var spans = Spans(export);
        var top = export.Items.Single(i => i.ItemId == "dev-001-Top");

        var children = spans.Where(s => (string?)s["parentSpanId"] == top.RootSpanId).ToList();

        Assert.Equal(
            new[] { "generate attempt 1", "generate attempt 2", "decode-clue #0", "decode-clue #1", "decode-clue #2" },
            children.Select(c => (string?)c["name"]));
        Assert.All(children, c => Assert.Equal(top.TraceId, (string?)c["traceId"]));
        Assert.All(children, c => Assert.Equal("generation", Attr(c, "langfuse.observation.type")));
    }

    [Fact]
    public void La_racine_porte_l_entree_la_sortie_retenue_et_la_paire_attendue()
    {
        var root = Spans(Build()).First(s => s["parentSpanId"] is null);

        Assert.Equal("dev-001", (string?)JsonNode.Parse(Attr(root, "langfuse.observation.input")!)!["boardId"]);
        Assert.Equal("Jardin", (string?)JsonNode.Parse(Attr(root, "langfuse.observation.output")!)!["clue"]);
        Assert.NotNull(JsonNode.Parse(Attr(root, "langfuse.experiment.item.expected_output")!)!["referenceWords"]);
    }

    [Fact]
    public void La_chronologie_enchaine_les_latences_consignees_depuis_la_creation_du_run()
    {
        var spans = Spans(Build());
        var runStart = OtlpExperimentBuilder.UnixNanos(new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc));
        var first = spans.First(s => (string?)s["name"] == "generate attempt 1");
        var second = spans.First(s => (string?)s["name"] == "generate attempt 2");

        Assert.Equal(runStart, (string?)first["startTimeUnixNano"]);
        Assert.Equal((long.Parse(runStart) + 1_000_000_000).ToString(), (string?)first["endTimeUnixNano"]);
        Assert.Equal((string?)first["endTimeUnixNano"], (string?)second["startTimeUnixNano"]);

        var topRoot = spans.First(s => s["parentSpanId"] is null);
        var lastTopChild = spans.Last(s => (string?)s["parentSpanId"] == (string?)topRoot["spanId"]);
        Assert.Equal((string?)lastTopChild["endTimeUnixNano"], (string?)topRoot["endTimeUnixNano"]);
    }

    [Fact]
    public void Deux_constructions_rendent_les_memes_identifiants_donc_un_reexport_idempotent()
    {
        Assert.Equal(
            Spans(Build()).Select(s => (string?)s["spanId"]),
            Spans(Build()).Select(s => (string?)s["spanId"]));
        Assert.Equal(32, OtlpIds.TraceId(ExperimentId, "dev-001-Top").Length);
        Assert.Equal(16, OtlpIds.SpanId(ExperimentId, "dev-001-Top", "root").Length);
        Assert.NotEqual(OtlpIds.TraceId(ExperimentId, "dev-001-Top"), OtlpIds.TraceId("autre", "dev-001-Top"));
    }

    [Fact]
    public void Une_direction_sans_decodage_garde_sa_racine_et_n_a_aucun_span_de_decodage()
    {
        var left = Build().Items.Single(i => i.ItemId == "dev-001-Left");

        Assert.Empty(left.Decodes);
    }

    [Fact]
    public void Les_decodages_exposent_leur_r_y_compris_nul_pour_un_echec_de_format()
    {
        var bottom = Build().Items.Single(i => i.ItemId == "dev-001-Bottom");

        Assert.Equal(new double?[] { 1.0, null, 0.5 }, bottom.Decodes.Select(d => d.R));
    }
}
