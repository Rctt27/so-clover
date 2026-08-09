using System.Globalization;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Calibration;

public sealed record Gate(string Name, double Value, double Threshold, string Comparison, bool Passed);

public sealed record CalibrationVerdict(
    IReadOnlyList<Gate> Gates, bool AllPassed, string Label, IReadOnlyList<string> Reasons);

/// <summary>
/// Les <b>quatre</b> portes d'acceptation du PRD, réunies en un verdict unique.
/// <para>
/// Sans cette agrégation, l'opérateur recolle quatre chiffres produits par trois commandes
/// différentes, et franchit « une porte sur trois » portes sur quatre sans que rien ne le
/// signale. Les seuils sont <b>repris verbatim</b> du tableau « Portes d'acceptation » : un seuil
/// qui dérive d'une spec à l'autre est exactement ce que le registre est censé rendre impossible.
/// </para>
/// </summary>
public static class CalibrationGates
{
    public const double MinAgreement = 0.75;
    public const double MinKappa = 0.40;
    public const double MaxSaturationRecovery = 0.95;
    public const double MaxFloorRecovery = 0.15;

    public const string AgreementGate = "accord";
    public const string KappaGate = "kappa";
    public const string SaturationGate = "non-saturation";
    public const string FloorGate = "plancher";

    public const string ValidatedLabel = "DÉCODEUR VALIDÉ";
    public const string RejectedLabel = "DÉCODEUR RENVOYÉ EN P3";

    /// <summary>Marge d'arrondi : un seuil est une règle éditoriale, l'égalité doit passer.</summary>
    private const double ThresholdTolerance = 1e-9;

    public static CalibrationVerdict Evaluate(
        AgreementReport agreement, double saturationRecovery, double floorRecovery)
    {
        var gates = new[]
        {
            AtLeast(AgreementGate, agreement.Agreement, MinAgreement),
            AtLeast(KappaGate, agreement.Kappa, MinKappa),
            AtMost(SaturationGate, saturationRecovery, MaxSaturationRecovery),
            AtMost(FloorGate, floorRecovery, MaxFloorRecovery),
        };

        var reasons = gates
            .Where(g => !g.Passed)
            .Select(g => string.Create(CultureInfo.GetCultureInfo("fr-FR"),
                $"porte non franchie : {g.Name} = {g.Value:0.000} ({g.Comparison} {g.Threshold:0.00} requis)"))
            .ToList();

        // Le dénominateur fragile n'est PAS une cinquième porte : le chiffre est publié, MARQUÉ.
        // Un accord de 0,80 sur 28 couples a un IC qui traverse la porte, et le rapport doit le dire.
        if (agreement.ThinDenominator)
            reasons.Add(
                $"⚠ dénominateur de l'accord : {agreement.DecidedCount} couple(s), " +
                $"sous les {AgreementMetrics.ThinDenominatorThreshold} attendus — chiffre FRAGILE, " +
                "lire l'IC avant de conclure");

        if (agreement.UnscorableCoupleCount > 0)
            reasons.Add(
                $"{agreement.UnscorableCoupleCount} couple(s) exclus : un indice sans aucun décodage exploitable");

        var allPassed = gates.All(g => g.Passed);
        return new CalibrationVerdict(
            gates.ToList().AsReadOnly(),
            allPassed,
            allPassed ? ValidatedLabel : RejectedLabel,
            reasons.AsReadOnly());
    }

    private static Gate AtLeast(string name, double value, double threshold) =>
        new(name, value, threshold, "≥", value >= threshold - ThresholdTolerance);

    private static Gate AtMost(string name, double value, double threshold) =>
        new(name, value, threshold, "≤", value <= threshold + ThresholdTolerance);

    // ── Empreintes ──────────────────────────────────────────────────────────

    private const string MetricsSuffix = ".metrics.json";
    private const string DecodedSuffix = ".decoded.jsonl";

    /// <summary>
    /// <c>&lt;run&gt;.metrics.json</c> → <c>&lt;run&gt;.decoded.jsonl</c>.
    /// <para>
    /// <c>MetricsReport</c> ne porte aucune empreinte de décodeur, et lui en ajouter une changerait
    /// le schéma des <c>.metrics.json</c> déjà produits. Le fichier frère porte déjà toute
    /// l'information — c'est lui qu'on interroge.
    /// </para>
    /// </summary>
    public static string DecodedPathForMetrics(string metricsPath)
    {
        if (!metricsPath.EndsWith(MetricsSuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Chemin de métriques attendu en « {MetricsSuffix} », reçu : {metricsPath}", nameof(metricsPath));

        return metricsPath[..^MetricsSuffix.Length] + DecodedSuffix;
    }

    /// <summary>Issue sur laquelle la porte de non-saturation est définie.</summary>
    public const string SaturationOutcome = "solide";

    /// <summary>
    /// Refuse des métriques de saturation qui ne portent pas <b>exactement</b> sur les indices
    /// humains <c>solide</c>.
    /// <para>
    /// <c>score</c> écrit toujours dans <c>&lt;run&gt;.metrics.json</c> : un second <c>score</c>
    /// sur le même pseudo-run, sans <c>--subset-outcome solide</c>, remplace le fichier sans rien
    /// signaler. La porte serait alors évaluée sur le plafond <i>joué</i> — <c>pass</c> compris,
    /// donc plus bas — et franchie pour la mauvaise raison. Même philosophie que
    /// <see cref="RequireSameDecoder"/> : refus bruyant plutôt que chiffre plausible.
    /// </para>
    /// </summary>
    public static void RequireSaturationSubset(string metricsPath)
    {
        var metrics = EvalJson.Deserialize<MetricsReport>(File.ReadAllText(metricsPath));

        if (metrics.SubsetOutcome is null)
            throw new InvalidOperationException(
                $"{metricsPath} ne déclare aucun filtre d'issues. La porte de non-saturation est " +
                $"définie sur les seuls indices « {SaturationOutcome} » : produire ce fichier avec " +
                $"`score --subset <élicitation> --subset-outcome {SaturationOutcome}`. " +
                "Un .metrics.json antérieur à l'ajout de la provenance ne prouve rien.");

        var outcomes = SubsetSelector.ParseOutcomes(metrics.SubsetOutcome);
        if (outcomes.Count != 1 || !outcomes.Contains(SaturationOutcome))
            throw new InvalidOperationException(
                $"{metricsPath} porte sur les issues « {metrics.SubsetOutcome} », la porte de " +
                $"non-saturation sur les seuls « {SaturationOutcome} ». Le plafond sur paires " +
                "résolues (A-3) et le plafond joué sont d'autres mesures — elles ne franchissent " +
                "pas cette porte.");
    }

    public static string FingerprintOfMetrics(string metricsPath) =>
        DecoderFingerprint.FromManifest(DecodeFile.Read(DecodedPathForMetrics(metricsPath)).Manifest);

    /// <summary>
    /// Refuse si l'une des métriques citées n'a pas été produite par le décodeur calibré. Les
    /// quatre portes doivent porter sur <b>le même</b> décodeur : deux <c>recovery</c> d'empreintes
    /// différentes ne se comparent pas, au même titre que deux runs de <c>benchHash</c> différents,
    /// que <c>compare</c> refuse déjà.
    /// </summary>
    public static void RequireSameDecoder(string fingerprint, params string[] metricsPaths)
    {
        foreach (var path in metricsPaths)
        {
            var other = FingerprintOfMetrics(path);
            if (!string.Equals(other, fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{path} a été produit par le décodeur d'empreinte {other}, la calibration porte " +
                    $"sur {fingerprint}. Les quatre portes doivent porter sur le MÊME décodeur — " +
                    "re-décoder ce run avec le décodeur courant (decode --force) puis le re-scorer.");
        }
    }
}
