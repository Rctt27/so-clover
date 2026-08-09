using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationGatesTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"gates-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static AgreementReport Agreement(
        double agreement = 0.82, double kappa = 0.55, int decided = 60) => new(
        CoupleCount: 100, DecidedCount: decided, UnscorableCoupleCount: 0,
        Agreement: agreement, AgreementCiLow: agreement - 0.08, AgreementCiHigh: agreement + 0.08,
        Kappa: kappa, KappaCiLow: kappa - 0.10, KappaCiHigh: kappa + 0.10,
        Pabak: 2 * agreement - 1,
        HumanTieRate: 0.10, DecoderTieRate: 0.30,
        Contingency: [], HumanMarginalA: 0.6, HumanMarginalB: 0.4,
        DecoderMarginalA: 0.58, DecoderMarginalB: 0.42,
        ByFamily: [], AnchorCount: 5, AnchorCorrect: 5,
        ThinDenominator: decided < 40);

    // Les quatre seuils sont REPRIS VERBATIM du tableau « Portes d'acceptation » du PRD.
    // Un seuil qui dérive d'une spec à l'autre est exactement ce que le registre est censé
    // rendre impossible — ce test est le verrou.
    [Fact]
    public void The_four_thresholds_match_the_PRD_verbatim()
    {
        Assert.Equal(0.75, CalibrationGates.MinAgreement);
        Assert.Equal(0.40, CalibrationGates.MinKappa);
        Assert.Equal(0.95, CalibrationGates.MaxSaturationRecovery);
        Assert.Equal(0.15, CalibrationGates.MaxFloorRecovery);
    }

    [Fact]
    public void All_four_gates_passed_yields_a_single_validated_verdict()
    {
        var verdict = CalibrationGates.Evaluate(Agreement(), saturationRecovery: 0.88, floorRecovery: 0.11);

        Assert.True(verdict.AllPassed);
        Assert.Equal(CalibrationGates.ValidatedLabel, verdict.Label);
        Assert.Equal(4, verdict.Gates.Count);
        Assert.All(verdict.Gates, g => Assert.True(g.Passed));
    }

    [Theory]
    [InlineData(0.74, 0.55, 0.88, 0.11, CalibrationGates.AgreementGate)]
    [InlineData(0.82, 0.31, 0.88, 0.11, CalibrationGates.KappaGate)]
    [InlineData(0.82, 0.55, 0.97, 0.11, CalibrationGates.SaturationGate)]
    [InlineData(0.82, 0.55, 0.88, 0.19, CalibrationGates.FloorGate)]
    public void Any_single_failing_gate_sends_the_decoder_back_to_P3(
        double agreement, double kappa, double saturation, double floor, string failing)
    {
        var verdict = CalibrationGates.Evaluate(Agreement(agreement, kappa), saturation, floor);

        Assert.False(verdict.AllPassed);
        Assert.Equal(CalibrationGates.RejectedLabel, verdict.Label);
        Assert.False(verdict.Gates.Single(g => g.Name == failing).Passed);
        Assert.Contains(verdict.Reasons, r => r.Contains(failing, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_failing_gate_is_named_when_several_fall_at_once()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(agreement: 0.60, kappa: 0.20), saturationRecovery: 0.99, floorRecovery: 0.40);

        Assert.Equal(4, verdict.Reasons.Count(r => r.StartsWith("porte non franchie", StringComparison.Ordinal)));
        Assert.All(verdict.Gates, g => Assert.False(g.Passed));
    }

    [Fact]
    public void A_threshold_met_exactly_passes()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(agreement: CalibrationGates.MinAgreement, kappa: CalibrationGates.MinKappa),
            saturationRecovery: CalibrationGates.MaxSaturationRecovery,
            floorRecovery: CalibrationGates.MaxFloorRecovery);

        Assert.True(verdict.AllPassed);
    }

    // Le dénominateur fragile n'est PAS une cinquième porte : le chiffre est publié, marqué.
    [Fact]
    public void A_thin_denominator_is_reported_without_becoming_a_fifth_gate()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(decided: 28), saturationRecovery: 0.88, floorRecovery: 0.11);

        Assert.True(verdict.AllPassed);
        Assert.Contains(verdict.Reasons, r => r.Contains("28"));
    }

    // ---- Empreintes ------------------------------------------------------------

    [Fact]
    public void DecodedPathForMetrics_finds_the_sibling_decoded_file()
    {
        Assert.Equal(
            Path.Combine("eval", "runs", "run-x.decoded.jsonl"),
            CalibrationGates.DecodedPathForMetrics(Path.Combine("eval", "runs", "run-x.metrics.json")));
    }

    // Une porte franchie sur un instrument et des chiffres publiés par un autre : c'est
    // exactement l'accident que le cycle vient réparer. Il doit être impossible, pas déconseillé.
    [Fact]
    public void Refuses_metrics_produced_by_another_decoder()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 1);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2));

        var ex = Assert.Throws<InvalidOperationException>(
            () => CalibrationGates.RequireSameDecoder(fingerprint, metrics));

        Assert.Contains("empreinte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_metrics_produced_by_the_same_decoder()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 2);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2));

        CalibrationGates.RequireSameDecoder(fingerprint, metrics);
    }

    // decodesPerClue à 5 en calibration et 3 dans les runs : les empreintes ne divergent pas.
    [Fact]
    public void A_different_decodes_per_clue_does_not_break_the_fingerprint_check()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 2, decodesPerClue: 3);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2, decodesPerClue: 5));

        CalibrationGates.RequireSameDecoder(fingerprint, metrics);
    }

    [Fact]
    public void Refuses_metrics_whose_decoded_sibling_is_missing()
    {
        var metrics = Path.Combine(_directory, "orphelin.metrics.json");
        File.WriteAllText(metrics, "{}");

        Assert.Throws<FileNotFoundException>(
            () => CalibrationGates.RequireSameDecoder("3f2a91c4e0d1", metrics));
    }

    private string WriteMetricsWithDecoder(
        string name, int? cluePromptVersion, int decodesPerClue = 3)
    {
        var metrics = Path.Combine(_directory, $"{name}.metrics.json");
        File.WriteAllText(metrics, "{}");

        DecodeFile.WriteManifest(
            Path.Combine(_directory, $"{name}.decoded.jsonl"),
            DecoderFingerprintTests.Manifest(
                cluePromptVersion: cluePromptVersion, decodesPerClue: decodesPerClue));

        return metrics;
    }
}
