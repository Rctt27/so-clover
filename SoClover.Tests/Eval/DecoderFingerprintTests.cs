using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class DecoderFingerprintTests
{
    private static string Fingerprint(
        string modelId = "qwen/qwen3-8b",
        string promptFile = "C:/build/Decoder/Prompts/fr/decode-clue.md",
        int? version = 2,
        double temperature = 0.3,
        double? topP = null,
        int? maxOutputTokens = 512) =>
        DecoderFingerprint.Compute(modelId, promptFile, version, temperature, topP, maxOutputTokens);

    [Fact]
    public void Is_twelve_lowercase_hex_characters()
    {
        var fingerprint = Fingerprint();

        Assert.Equal(DecoderFingerprint.HexLength, fingerprint.Length);
        Assert.Matches("^[0-9a-f]{12}$", fingerprint);
    }

    [Fact]
    public void Is_stable_for_an_identical_configuration()
    {
        Assert.Equal(Fingerprint(), Fingerprint());
    }

    [Theory]
    [InlineData("autre-modele", null, null, null, null)]
    [InlineData(null, "C:/build/Decoder/Prompts/en/decode-clue.md", null, null, null)]
    [InlineData(null, null, 3, null, null)]
    [InlineData(null, null, null, 0.7, null)]
    [InlineData(null, null, null, null, 1024)]
    public void Changes_on_each_of_the_configuration_fields(
        string? modelId, string? promptFile, int? version, double? temperature, int? maxOutputTokens)
    {
        var changed = DecoderFingerprint.Compute(
            modelId ?? "qwen/qwen3-8b",
            promptFile ?? "C:/build/Decoder/Prompts/fr/decode-clue.md",
            version ?? 2,
            temperature ?? 0.3,
            null,
            maxOutputTokens ?? 512);

        Assert.NotEqual(Fingerprint(), changed);
    }

    [Fact]
    public void Changes_when_topP_moves_from_null_to_a_value()
    {
        Assert.NotEqual(Fingerprint(), Fingerprint(topP: 0.95));
    }

    // C'EST LE POINT DU CYCLE : le décodeur qui a produit recovery = 0,363 portait
    // cluePromptVersion 1 ; le prompt est en v2. Deux recovery d'empreintes différentes ne
    // se comparent pas, au même titre que deux runs de benchHash différents.
    [Fact]
    public void The_prompt_version_alone_changes_the_fingerprint()
    {
        Assert.NotEqual(Fingerprint(version: 1), Fingerprint(version: 2));
    }

    // decodesPerClue change la GRANULARITÉ de R̄, pas le décodeur : c'est ce qui autorise une
    // calibration à 5 décodages et des runs à 3 sans que les empreintes divergent.
    [Fact]
    public void The_number_of_decodes_per_clue_is_deliberately_excluded()
    {
        var three = Manifest(decodesPerClue: 3);
        var five = Manifest(decodesPerClue: 5);

        Assert.Equal(DecoderFingerprint.FromManifest(three), DecoderFingerprint.FromManifest(five));
    }

    // Le chemin du prompt est ABSOLU dans le manifeste (dérivé d'AppContext.BaseDirectory).
    // Haché tel quel, l'empreinte changerait d'une machine à l'autre — et deux calibrations
    // identiques deviendraient incomparables.
    [Fact]
    public void Two_machines_with_different_base_directories_share_the_fingerprint()
    {
        var windows = Fingerprint(promptFile: "C:/src/bin/Debug/net9.0/Decoder/Prompts/fr/decode-clue.md");
        var linux = Fingerprint(promptFile: "/home/ci/app/Decoder/Prompts/fr/decode-clue.md");

        Assert.Equal(windows, linux);
    }

    [Fact]
    public void Backslashes_and_casing_do_not_change_the_canonical_prompt_path()
    {
        Assert.Equal(
            DecoderFingerprint.CanonicalPromptPath(@"C:\build\Decoder\Prompts\FR\Decode-Clue.md"),
            DecoderFingerprint.CanonicalPromptPath("/opt/app/Decoder/Prompts/fr/decode-clue.md"));
    }

    [Fact]
    public void A_bare_file_name_is_accepted_as_its_own_canonical_path()
    {
        Assert.Equal("decode-clue.md", DecoderFingerprint.CanonicalPromptPath("decode-clue.md"));
    }

    [Fact]
    public void FromManifest_matches_Compute_on_the_same_fields()
    {
        var manifest = Manifest();

        Assert.Equal(
            DecoderFingerprint.Compute(
                manifest.ModelId, manifest.CluePromptFile, manifest.CluePromptVersion,
                manifest.Temperature, manifest.TopP, manifest.MaxOutputTokens),
            DecoderFingerprint.FromManifest(manifest));
    }

    internal static DecodeManifest Manifest(
        string modelId = "qwen/qwen3-8b",
        int? cluePromptVersion = 2,
        int decodesPerClue = 3,
        double temperature = 0.3) => new(
        Kind: "manifest",
        DecodeRunId: "run+decode-20260805120000",
        CreatedAtUtc: new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc),
        GeneratorRunId: "run",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: modelId,
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "0123456789ab",
        Temperature: temperature,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "C:/build/Decoder/Prompts/fr/decode-clue.md",
        CluePromptVersion: cluePromptVersion,
        BoardPromptFile: "C:/build/Decoder/Prompts/fr/decode-board.md",
        BoardPromptVersion: 1,
        DecodesPerClue: decodesPerClue,
        HarnessVersion: RunFile.HarnessVersion,
        OperatorNotes: null);
}
