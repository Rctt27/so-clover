using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class BenchFileTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static BenchContents Sample(int boardCount = 4) =>
        BenchGenerator.Generate(
            "dev", 20260726001, boardCount,
            Enumerable.Range(1, 100).Select(i => $"Mot{i:D3}").ToList(),
            "Français_OFF", "Français_OFF.txt", "dicthash1234",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Round_trips_a_bench_without_loss()
    {
        var original = Sample();

        BenchFile.Write(_path, original);
        var reloaded = BenchFile.Read(_path);

        Assert.Equal(original.Manifest, reloaded.Manifest);
        Assert.Equal(original.Boards.Count, reloaded.Boards.Count);
        for (var i = 0; i < original.Boards.Count; i++)
        {
            Assert.Equal(original.Boards[i].BoardId, reloaded.Boards[i].BoardId);
            Assert.Equal(original.Boards[i].Cards, reloaded.Boards[i].Cards);
            Assert.Equal(
                original.Boards[i].Directions.Select(d => d.ReferenceWords),
                reloaded.Boards[i].Directions.Select(d => d.ReferenceWords));
        }
    }

    [Fact]
    public void Writes_the_manifest_on_the_first_line_and_one_board_per_line()
    {
        var bench = Sample(boardCount: 4);

        BenchFile.Write(_path, bench);

        var lines = File.ReadAllLines(_path);
        Assert.Equal(5, lines.Length);
        Assert.Contains("\"kind\":\"manifest\"", lines[0]);
        Assert.All(lines.Skip(1), l => Assert.Contains("\"kind\":\"board\"", l));
    }

    [Fact]
    public void Preserves_accented_words_unescaped_for_human_review()
    {
        var bench = BenchGenerator.Generate(
            "dev", 1, 1,
            ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
             "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont"],
            "Français_OFF", "Français_OFF.txt", "h", new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

        BenchFile.Write(_path, bench);

        var text = File.ReadAllText(_path);
        Assert.Contains("Forêt", text);
        Assert.DoesNotContain("\\u00", text);
    }

    // Un banc qui bouge invalide tout l'historique du registre : la lecture doit refuser,
    // pas avertir.
    [Fact]
    public void Refuses_a_bench_whose_content_drifted_from_its_declared_hash()
    {
        BenchFile.Write(_path, Sample());
        var lines = File.ReadAllLines(_path);

        // Sample() tire 16 mots parmi 100 : rien ne garantit que "Mot001" atterrisse sur le
        // premier board pour une seed donnée. On mute un mot dont on a prouvé la présence dans
        // lines[1] plutôt qu'un littéral supposé, pour que la mutation dérive bien le hash.
        var firstBoard = EvalJson.Deserialize<BenchBoard>(lines[1]);
        var wordToMutate = firstBoard.Cards[0][0];
        lines[1] = lines[1].Replace($"\"{wordToMutate}\"", "\"MotXXX\"");
        File.WriteAllLines(_path, lines);

        var ex = Assert.Throws<BenchIntegrityException>(() => BenchFile.Read(_path));
        Assert.Contains("benchHash", ex.Message);
    }

    [Fact]
    public void Refuses_a_bench_whose_board_count_contradicts_its_manifest()
    {
        BenchFile.Write(_path, Sample(boardCount: 4));
        var lines = File.ReadAllLines(_path).ToList();
        lines.RemoveAt(lines.Count - 1);
        File.WriteAllLines(_path, lines);

        Assert.Throws<BenchIntegrityException>(() => BenchFile.Read(_path));
    }

    [Fact]
    public void Refuses_a_file_whose_first_line_is_not_a_manifest()
    {
        File.WriteAllText(_path, "{\"kind\":\"board\",\"boardId\":\"x\"}\n");

        Assert.Throws<BenchIntegrityException>(() => BenchFile.Read(_path));
    }

    [Fact]
    public void ComputeBenchHash_is_stable_and_twelve_hex_characters()
    {
        var bench = Sample();

        var hash = BenchFile.ComputeBenchHash(bench.Boards);

        Assert.Equal(12, hash.Length);
        Assert.Matches("^[0-9a-f]{12}$", hash);
        Assert.Equal(bench.Manifest.BenchHash, hash);
        Assert.Equal(hash, BenchFile.ComputeBenchHash(bench.Boards));
    }
}
