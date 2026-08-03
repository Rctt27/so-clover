using SoClover.Eval.Bench;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Régression : un <c>.metrics.json</c> absent (porte de saturation ou de plancher) doit
/// interrompre <c>calibrate</c> AVANT tout appel au décodeur, pas après la boucle de décodage
/// complète. <c>RequireSameDecoder</c> ne lit que le <c>.decoded.jsonl</c> frère — il ne prouve
/// pas que le <c>.metrics.json</c> lui-même existe. La preuve porte sur un effet de bord
/// observable sans appel réseau : le manifeste de calibration ne s'écrit qu'APRÈS la lecture des
/// deux <c>recovery</c> externes, donc son absence prouve que la commande a échoué avant la
/// moindre dépense (fetch du hash de modèles compris).
/// </summary>
public class CalibrateCommandMetricsGuardTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"calibrate-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task A_missing_saturation_metrics_file_stops_the_command_before_any_decode_attempt()
    {
        // Empreinte du décodeur COURANT (config + prompt réels), calculée exactement comme
        // CalibrateCommand la calcule : c'est ce qui permet à RequireSameDecoder de passer et
        // d'atteindre le point corrigé, sans quoi ce test ne prouverait rien sur l'ordre.
        var opts = EvalLlmConfig.Bind(EvalLlmConfig.BuildConfiguration(), "Decoder").Value;
        var cluePromptPath = Path.Combine(
            AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");
        var promptVersion = new FilePromptLoader().Load(cluePromptPath).Version;

        var decodedManifest = new DecodeManifest(
            Kind: "manifest", DecodeRunId: "run+decode-x", CreatedAtUtc: DateTime.UtcNow,
            GeneratorRunId: "run", BenchFile: "eval/boards.dev.jsonl", BenchHash: "aaaaaaaaaaaa",
            Provider: opts.Provider.ToString(), BaseUrl: opts.BaseUrl, ModelId: opts.DefaultModel,
            ModelSnapshotDate: null, ProviderModelListHash: null, Temperature: opts.DefaultTemperature,
            TopP: opts.TopP, MaxOutputTokens: opts.MaxOutputTokens,
            CluePromptFile: cluePromptPath.Replace('\\', '/'), CluePromptVersion: promptVersion,
            BoardPromptFile: "fr/decode-board.md", BoardPromptVersion: 1, DecodesPerClue: 3,
            HarnessVersion: RunFile.HarnessVersion, OperatorNotes: null);

        // Le .decoded.jsonl frère existe et porte la bonne empreinte pour LES DEUX portes —
        // RequireSameDecoder doit passer. Mais AUCUN .metrics.json n'est jamais créé pour la
        // saturation : c'est exactement le cas signalé (run décodé, pas encore scoré).
        var saturationMetrics = Path.Combine(_directory, "saturation.metrics.json");
        DecodeFile.WriteManifest(Path.Combine(_directory, "saturation.decoded.jsonl"), decodedManifest);

        var floorMetrics = Path.Combine(_directory, "floor.metrics.json");
        File.WriteAllText(floorMetrics, "{}");
        DecodeFile.WriteManifest(Path.Combine(_directory, "floor.decoded.jsonl"), decodedManifest);

        // HumanTestData.Bench place un benchHash de convenance dans le manifeste, non recalculé
        // à partir des boards : suffisant pour les tests qui restent en mémoire, mais BenchFile.Read
        // (emprunté ici pour de vrai, contrairement au reste de la suite calibration) exige le
        // vrai hash — on le recalcule avec la même fonction que l'écrivain.
        var boardsOnly = HumanTestData.Bench(boardCount: 1).Boards;
        var benchHash = BenchFile.ComputeBenchHash(boardsOnly);
        var bench = HumanTestData.Bench(boardCount: 1, benchHash: benchHash);
        var benchPath = Path.Combine(_directory, "boards.dev.jsonl");
        BenchFile.Write(benchPath, bench);

        var comparisonsPath = Path.Combine(_directory, "comparisons.dev.jsonl");
        HumanFile.WriteComparisonManifest(comparisonsPath, HumanTestData.ComparisonManifest(benchHash, targetCount: 1));
        HumanFile.AppendComparison(comparisonsPath, HumanTestData.Comparison("c-001", JudgeSession.VerdictA));

        var outDirectory = Path.Combine(_directory, "out");
        var args = Args.Parse(
        [
            "calibrate",
            "--comparisons", comparisonsPath,
            "--bench", benchPath,
            "--out", outDirectory,
            "--saturation-metrics", saturationMetrics,
            "--floor-metrics", floorMetrics,
        ]);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => CalibrateCommand.ExecuteAsync(args, CancellationToken.None));

        // Si le manifeste de calibration existait, la boucle de décodage aurait forcément
        // commencé (le manifeste s'écrit juste avant elle) : sa seule absence suffit à prouver
        // qu'aucun décodage — et aucun appel réseau — n'a été tenté.
        Assert.False(Directory.Exists(outDirectory), "aucun fichier ne doit être écrit avant l'échec");
    }
}
