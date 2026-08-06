using SoClover.Eval.Calibration;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Ce que le serveur rapporte du modèle <b>chargé</b>, par opposition à ce que la requête demande.
/// <para>
/// Constat du 2026-08-06 : <c>ChatOptions</c> ne transmet que <c>ModelId</c>, <c>Temperature</c>,
/// <c>TopP</c> et <c>MaxOutputTokens</c>. Tout le reste — quantification, longueur de contexte
/// chargée, <c>top_k</c>, <c>repeat_penalty</c> — vient des réglages LM Studio, qui sont
/// <b>par modèle</b>. Deux calibrations pouvaient donc différer par la seule quantification sans
/// qu'aucun artefact ne le montre.
/// </para>
/// <para>
/// La quantification et le contexte chargé sont, eux, <b>observables</b> via l'endpoint natif
/// <c>/api/v0/models</c>. On les consigne. Ils restent <b>hors empreinte</b> : l'empreinte dit ce
/// qu'on a demandé au décodeur, ces champs disent ce que la machine a servi.
/// </para>
/// </summary>
public class ModelRuntimeProbeTests
{
    private const string Payload = """
        {
          "data": [
            {
              "id": "mistralai/ministral-3-14b-reasoning",
              "state": "loaded",
              "quantization": "Q4_K_M",
              "max_context_length": 262144,
              "loaded_context_length": 4096
            },
            {
              "id": "qwen/qwen3-8b",
              "state": "not-loaded",
              "quantization": "Q8_0",
              "max_context_length": 32768
            }
          ]
        }
        """;

    [Fact]
    public void Lit_la_quantification_et_le_contexte_charge_du_modele_demande()
    {
        var info = ModelRuntimeProbe.Parse(Payload, "mistralai/ministral-3-14b-reasoning");

        Assert.Equal("Q4_K_M", info.Quantization);
        Assert.Equal(4096, info.LoadedContextLength);
    }

    /// <summary>
    /// Un modèle présent au catalogue mais non chargé n'a pas de contexte chargé. Le champ reste
    /// nul plutôt que de recopier <c>max_context_length</c>, qui décrit une capacité et non la
    /// configuration effective.
    /// </summary>
    [Fact]
    public void Un_modele_non_charge_n_a_pas_de_contexte_charge()
    {
        var info = ModelRuntimeProbe.Parse(Payload, "qwen/qwen3-8b");

        Assert.Equal("Q8_0", info.Quantization);
        Assert.Null(info.LoadedContextLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("{}")]
    [InlineData("""{"data": []}""")]
    [InlineData("""{"data": [{"id": "un-autre-modele"}]}""")]
    public void Un_payload_inexploitable_rend_l_information_absente_sans_lever(string payload)
    {
        var info = ModelRuntimeProbe.Parse(payload, "mistralai/ministral-3-14b-reasoning");

        Assert.Null(info.Quantization);
        Assert.Null(info.LoadedContextLength);
    }

    /// <summary>
    /// <c>baseUrl</c> pointe l'API compatible OpenAI (<c>…/v1</c>) ; l'endpoint qui porte ces
    /// champs est l'API native de LM Studio (<c>…/api/v0/models</c>). La racine se dérive.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:1234/v1", "http://localhost:1234/api/v0/models")]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/api/v0/models")]
    [InlineData("http://host.docker.internal:1234/v1", "http://host.docker.internal:1234/api/v0/models")]
    [InlineData("http://localhost:1234", "http://localhost:1234/api/v0/models")]
    public void Derive_l_endpoint_natif_depuis_l_url_compatible_openai(string baseUrl, string expected)
    {
        Assert.Equal(expected, ModelRuntimeProbe.EndpointFor(baseUrl));
    }

    /// <summary>
    /// <b>L'invariant à ne jamais casser.</b> Ces champs décrivent la machine, pas la demande.
    /// Les glisser dans l'empreinte invaliderait d'un coup tous les artefacts déjà produits — et
    /// c'est exactement le genre d'ajout qu'on fait distraitement six mois plus tard.
    /// </summary>
    [Fact]
    public void La_quantification_et_le_contexte_ne_changent_pas_l_empreinte()
    {
        var baseline = Manifest(quantization: null, loadedContextLength: null);
        var probed = Manifest(quantization: "Q4_K_M", loadedContextLength: 4096);
        var other = Manifest(quantization: "Q8_0", loadedContextLength: 32768);

        var expected = DecoderFingerprint.FromManifest(baseline);

        Assert.Equal(expected, DecoderFingerprint.FromManifest(probed));
        Assert.Equal(expected, DecoderFingerprint.FromManifest(other));
    }

    private static DecodeManifest Manifest(string? quantization, int? loadedContextLength) =>
        new(
            Kind: "manifest",
            DecodeRunId: "run+decode-1",
            CreatedAtUtc: new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            GeneratorRunId: "run",
            BenchFile: "eval/boards.dev.jsonl",
            BenchHash: "416b819a41a1",
            Provider: "OpenAI",
            BaseUrl: "http://localhost:1234/v1",
            ModelId: "mistralai/ministral-3-14b-reasoning",
            ModelSnapshotDate: "2026-08-06",
            ProviderModelListHash: "8031e925d18b",
            Temperature: 0.3,
            TopP: null,
            MaxOutputTokens: 512,
            CluePromptFile: "/x/Decoder/Prompts/fr/decode-clue.md",
            CluePromptVersion: 2,
            BoardPromptFile: "/x/Decoder/Prompts/fr/decode-board.md",
            BoardPromptVersion: 1,
            DecodesPerClue: 3,
            HarnessVersion: 1,
            OperatorNotes: null,
            Quantization: quantization,
            LoadedContextLength: loadedContextLength);
}
