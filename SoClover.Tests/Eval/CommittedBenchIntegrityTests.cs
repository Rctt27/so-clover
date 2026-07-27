using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Infrastructure;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Verrou des bancs committés. Un banc qui bouge invalide tout l'historique du registre :
/// ces tests transforment une dérive silencieuse en échec de build.
/// </summary>
public class CommittedBenchIntegrityTests
{
    // Recopiés depuis les benchHash affichés par le verbe `bench` (regénéré après exclusion de
    // `strata` du périmètre du hash — voir BenchFile.ComputeBenchHash).
    private const string DevBenchHash = "416b819a41a1";
    private const string TestBenchHash = "1436bb07dc0d";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SoClover.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable.");
    }

    private static string BenchPath(string name) => Path.Combine(RepoRoot(), "eval", name);

    [Theory]
    [InlineData("boards.dev.jsonl", "dev", 40, 20260726001L)]
    [InlineData("boards.test.jsonl", "test", 60, 20260726002L)]
    public void Committed_bench_loads_and_matches_its_declared_shape(
        string fileName, string benchId, int boardCount, long seed)
    {
        var bench = BenchFile.Read(BenchPath(fileName));

        Assert.Equal(benchId, bench.Manifest.BenchId);
        Assert.Equal(boardCount, bench.Manifest.BoardCount);
        Assert.Equal(boardCount, bench.Boards.Count);
        Assert.Equal(seed, bench.Manifest.Seed);
        Assert.Equal(Xoshiro256SS.AlgorithmName, bench.Manifest.PrngAlgorithm);
        Assert.Equal(BenchGenerator.GeneratorVersion, bench.Manifest.GeneratorVersion);
        Assert.Equal("Français_OFF", bench.Manifest.Language);
    }

    [Theory]
    [InlineData("boards.dev.jsonl", DevBenchHash)]
    [InlineData("boards.test.jsonl", TestBenchHash)]
    public void Committed_bench_hash_is_frozen(string fileName, string expectedHash)
    {
        var bench = BenchFile.Read(BenchPath(fileName));

        Assert.Equal(expectedHash, bench.Manifest.BenchHash);
    }

    // Régénérer au même seed doit redonner exactement le même banc, sinon la reproductibilité
    // annoncée dans le manifeste est fausse.
    [Theory]
    [InlineData("boards.dev.jsonl")]
    [InlineData("boards.test.jsonl")]
    public async Task Regenerating_at_the_same_seed_reproduces_the_committed_bench(string fileName)
    {
        var committed = BenchFile.Read(BenchPath(fileName));

        var dictionaryDir = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Dictionaries");
        var words = await new FileWordDictionary(dictionaryDir)
            .GetAllWordsAsync(committed.Manifest.Language);

        var regenerated = BenchGenerator.Generate(
            committed.Manifest.BenchId,
            committed.Manifest.Seed,
            committed.Manifest.BoardCount,
            words,
            committed.Manifest.Language,
            committed.Manifest.DictionaryFile,
            committed.Manifest.DictionaryHash,
            committed.Manifest.CreatedAtUtc);

        Assert.Equal(committed.Manifest.BenchHash, regenerated.Manifest.BenchHash);
    }

    [Fact]
    public void Every_direction_of_every_board_carries_exactly_two_reference_words()
    {
        foreach (var fileName in new[] { "boards.dev.jsonl", "boards.test.jsonl" })
        {
            var bench = BenchFile.Read(BenchPath(fileName));
            foreach (var board in bench.Boards)
            {
                Assert.Equal(4, board.Directions.Count);
                Assert.All(board.Directions, d => Assert.Equal(2, d.ReferenceWords.Count));
                Assert.Equal(16, BenchBoardMapper.AllWords(board).Distinct().Count());
            }
        }
    }
}
