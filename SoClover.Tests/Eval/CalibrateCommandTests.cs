using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// I5 : <c>DefaultDecodesPerClue</c> et la garde de reprise D8 n'étaient protégés par aucun
/// test. <see cref="CalibrateCommand.RequireCompatibleResume"/> est une statique <b>pure</b> —
/// aucun <c>IChatClient</c>, aucun réseau : si un test avait besoin de l'un des deux, ce serait
/// le signe d'une mauvaise couture, pas d'un manque de fixture.
/// </summary>
public class CalibrateCommandTests
{
    // Le défaut de calibrate (5) et le défaut de decode (3) sont volontairement différents —
    // cf. le commentaire sur DefaultDecodesPerClue. Un verrou dans le style des autres verrous
    // du cycle (CalibrationGatesTests, AnalysisSampleTests, FailureTaxonomyTests).
    [Fact]
    public void The_calibrate_default_is_five_decodes_per_clue()
    {
        Assert.Equal(5, CalibrateCommand.DefaultDecodesPerClue);
    }

    [Fact]
    public void The_decode_default_remains_three_decodes_per_clue()
    {
        Assert.Equal(3, DecodeCommand.DefaultDecodesPerClue);
    }

    // ---- D8 : garde de reprise ---------------------------------------------------------

    // Le chemin du fichier de calibration fautif doit rester DANS le message : l'opérateur qui
    // reprend une calibration doit savoir QUEL fichier refuse, pas seulement pourquoi.
    private const string SamplePath = "eval/human/calibration.20260805-3f2a91c4e0d1.jsonl";

    [Fact]
    public void A_divergent_decodes_per_clue_refuses_names_the_file_and_points_to_force()
    {
        var existing = Manifest(decodesPerClue: 5, epsilon: 0.0);

        var ex = Assert.Throws<InvalidOperationException>(
            () => CalibrateCommand.RequireCompatibleResume(
                existing, decodesPerClue: 3, epsilon: 0.0, SamplePath));

        Assert.Contains("--force", ex.Message, StringComparison.Ordinal);
        Assert.Contains(SamplePath, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_divergent_epsilon_refuses()
    {
        var existing = Manifest(decodesPerClue: 5, epsilon: 0.0);

        Assert.Throws<InvalidOperationException>(
            () => CalibrateCommand.RequireCompatibleResume(
                existing, decodesPerClue: 5, epsilon: 0.2, SamplePath));
    }

    [Fact]
    public void Identical_values_pass_without_throwing()
    {
        var existing = Manifest(decodesPerClue: 5, epsilon: 0.0);

        CalibrateCommand.RequireCompatibleResume(existing, decodesPerClue: 5, epsilon: 0.0, SamplePath);
    }

    private static CalibrationManifest Manifest(int decodesPerClue, double epsilon) => new(
        Kind: "manifest",
        CalibrationId: "20260805-3f2a91c4e0d1",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.dev.jsonl",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "aaaaaaaaaaaa",
        CoupleAndAnchorCount: 100,
        ClueCount: 187,
        DecoderFingerprint: "3f2a91c4e0d1",
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "qwen/qwen3-8b",
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "0123456789ab",
        Temperature: 0.3,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "C:/build/Decoder/Prompts/fr/decode-clue.md",
        CluePromptVersion: 2,
        DecodesPerClue: decodesPerClue,
        Epsilon: epsilon,
        HarnessVersion: CalibrationFile.HarnessVersion,
        OperatorNotes: null);
}
