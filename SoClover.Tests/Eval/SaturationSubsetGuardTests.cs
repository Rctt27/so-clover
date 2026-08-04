using SoClover.Eval.Calibration;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// La porte de non-saturation est définie sur les indices humains <c>solide</c> — « le meilleur
/// indice atteignable ». Mesurée sur toutes les issues (<c>pass</c> compris) elle porterait sur
/// le plafond <i>joué</i>, plus bas, et franchirait le seuil pour la mauvaise raison.
/// <para>
/// Le risque n'est pas théorique : <c>score</c> écrit toujours dans
/// <c>&lt;run&gt;.metrics.json</c>, si bien qu'un second <c>score</c> sans
/// <c>--subset-outcome solide</c> remplace silencieusement le fichier que
/// <c>calibrate --saturation-metrics</c> ira lire.
/// </para>
/// </summary>
public class SaturationSubsetGuardTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"satur-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string WriteMetrics(string name, string? subsetFile, string? subsetOutcome)
    {
        var report = new MetricsReport(
            RunId: "human-20260804-e59651fc",
            BenchFile: "eval/boards.dev.jsonl",
            BenchHash: "416b819a41a1",
            BoardCount: 17,
            DirectionCount: 22,
            ValidRate: 1.0,
            FirstAttemptRate: 1.0,
            ParseFailureRate: 0.0,
            Recovery: 0.341,
            Strict2Of2: 0.0,
            HalfRate: 0.455,
            BoardPositions: 0.0,
            BoardSolvedFirstTry: 0.0,
            ConfusionTop: [],
            DecodeFailureRate: 0.0,
            ItemsCompleted: 22,
            ItemsExpected: 22,
            PerItemRBar: new Dictionary<(string, string), double>(),
            Counts: MetricCounts.Zero,
            SubsetFile: subsetFile,
            SubsetOutcome: subsetOutcome);

        var path = Path.Combine(_directory, $"{name}.metrics.json");
        File.WriteAllText(path, EvalJson.Serialize(report));
        return path;
    }

    [Fact]
    public void Accepte_des_metriques_restreintes_aux_seuls_indices_solide()
    {
        var path = WriteMetrics("plafond", "elicitation.dev.jsonl", "solide");

        CalibrationGates.RequireSaturationSubset(path);
    }

    // Le cas exact du 2026-08-04 : le dernier `score` lancé était celui du plafond joué,
    // et le fichier qui en résulte ne porte plus sur les indices solide.
    [Fact]
    public void Refuse_des_metriques_portant_sur_toutes_les_issues()
    {
        var path = WriteMetrics("plafond", "elicitation.dev.jsonl", subsetOutcome: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => CalibrationGates.RequireSaturationSubset(path));

        Assert.Contains("solide", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Le plafond A-3 (paires résolues) n'est pas la porte de saturation.
    [Fact]
    public void Refuse_des_metriques_portant_sur_solide_et_tiede()
    {
        var path = WriteMetrics("plafond", "elicitation.dev.jsonl", "solide,tiede");

        Assert.Throws<InvalidOperationException>(
            () => CalibrationGates.RequireSaturationSubset(path));
    }

    // Un .metrics.json produit avant l'ajout de la provenance ne prouve rien : le refuser
    // vaut mieux que de franchir une porte sur un chiffre invérifiable.
    [Fact]
    public void Refuse_des_metriques_sans_provenance_de_sous_ensemble()
    {
        var path = WriteMetrics("legacy", subsetFile: null, subsetOutcome: null);

        Assert.Throws<InvalidOperationException>(
            () => CalibrationGates.RequireSaturationSubset(path));
    }
}
