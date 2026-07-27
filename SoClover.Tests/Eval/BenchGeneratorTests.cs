using SoClover.Domain;
using SoClover.Eval.Bench;
using Xunit;

namespace SoClover.Tests.Eval;

public class BenchGeneratorTests
{
    private static IReadOnlyList<string> Dictionary(int size = 880) =>
        Enumerable.Range(1, size).Select(i => $"Mot{i:D3}").ToList();

    private static BenchContents Generate(int boardCount = 3, long seed = 20260726001) =>
        BenchGenerator.Generate(
            benchId: "dev",
            seed: seed,
            boardCount: boardCount,
            dictionary: Dictionary(),
            language: "Français_OFF",
            dictionaryFile: "Français_OFF.txt",
            dictionaryHash: "abcdef123456",
            createdAtUtc: new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Produces_the_requested_number_of_boards()
    {
        var bench = Generate(boardCount: 40);

        Assert.Equal(40, bench.Boards.Count);
        Assert.Equal(40, bench.Manifest.BoardCount);
    }

    [Fact]
    public void Board_ids_are_prefixed_by_the_bench_id_and_one_based()
    {
        var bench = Generate(boardCount: 3);

        Assert.Equal(["dev-001", "dev-002", "dev-003"], bench.Boards.Select(b => b.BoardId));
    }

    [Fact]
    public void Each_board_carries_four_cards_of_four_distinct_words()
    {
        var bench = Generate(boardCount: 10);

        foreach (var board in bench.Boards)
        {
            Assert.Equal(4, board.Cards.Count);
            Assert.All(board.Cards, card => Assert.Equal(4, card.Count));

            var words = board.Cards.SelectMany(c => c).ToList();
            Assert.Equal(16, words.Count);
            Assert.Equal(16, words.Distinct().Count());
        }
    }

    [Fact]
    public void Reference_words_follow_BoardGeometry_for_an_unrotated_board()
    {
        var bench = Generate(boardCount: 5);

        foreach (var board in bench.Boards)
        {
            Assert.Equal(4, board.Directions.Count);
            foreach (var dir in BoardGeometry.AllDirections)
            {
                var entry = board.Directions.Single(d => d.Direction == dir.ToString());
                var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(dir);

                Assert.Equal(
                    new[] { board.Cards[(int)cardA][(int)faceA], board.Cards[(int)cardB][(int)faceB] },
                    entry.ReferenceWords);
            }
        }
    }

    // Le harnais construit un CloverBoard réel à partir du banc : les paires de référence
    // stockées doivent coïncider avec ce que le domaine considère être le mot de l'indice.
    [Fact]
    public void Reference_words_match_the_domain_CloverBoard()
    {
        var board = Generate(boardCount: 1).Boards[0];
        var clover = BenchBoardMapper.ToCloverBoard(board);

        foreach (var dir in BoardGeometry.AllDirections)
        {
            var entry = board.Directions.Single(d => d.Direction == dir.ToString());
            Assert.Equal(entry.ReferenceWords[0], clover.GetClueText(dir));
        }
    }

    [Fact]
    public void Generation_is_reproducible_for_a_given_seed()
    {
        var a = Generate(boardCount: 12);
        var b = Generate(boardCount: 12);

        Assert.Equal(a.Manifest.BenchHash, b.Manifest.BenchHash);
        for (var i = 0; i < a.Boards.Count; i++)
            Assert.Equal(a.Boards[i].Cards, b.Boards[i].Cards);
    }

    [Fact]
    public void A_different_seed_yields_a_different_bench_hash()
    {
        var dev = Generate(boardCount: 12, seed: 20260726001);
        var test = Generate(boardCount: 12, seed: 20260726002);

        Assert.NotEqual(dev.Manifest.BenchHash, test.Manifest.BenchHash);
    }

    [Fact]
    public void Manifest_records_the_prng_and_generator_version()
    {
        var bench = Generate();

        Assert.Equal("manifest", bench.Manifest.Kind);
        Assert.Equal(Xoshiro256SS.AlgorithmName, bench.Manifest.PrngAlgorithm);
        Assert.Equal(BenchGenerator.GeneratorVersion, bench.Manifest.GeneratorVersion);
        Assert.Equal("Français_OFF", bench.Manifest.Language);
        Assert.Equal("Français_OFF.txt", bench.Manifest.DictionaryFile);
        Assert.Equal("abcdef123456", bench.Manifest.DictionaryHash);
        Assert.Equal(20260726001, bench.Manifest.Seed);
    }

    // Renseigné a posteriori par la séance A (P4) : le champ existe pour que l'enrichissement
    // ultérieur ne change pas de schéma.
    [Fact]
    public void Draw_difficulty_stratum_is_null_in_this_cycle()
    {
        var bench = Generate();

        Assert.All(bench.Boards, b => Assert.Null(b.Strata.DrawDifficulty));
    }

    [Fact]
    public void Rejects_a_dictionary_too_small_to_fill_a_board()
    {
        var ex = Assert.Throws<ArgumentException>(() => BenchGenerator.Generate(
            "dev", 1, 1, Dictionary(size: 15), "Français_OFF", "f.txt", "hash",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Contains("16", ex.Message);
    }

    [Fact]
    public void Rejects_a_non_positive_board_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BenchGenerator.Generate(
            "dev", 1, 0, Dictionary(), "Français_OFF", "f.txt", "hash",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Boards_are_not_all_identical()
    {
        var bench = Generate(boardCount: 5);

        var firstWords = bench.Boards.Select(b => b.Cards[0][0]).Distinct().Count();
        Assert.True(firstWords > 1, "tous les boards partagent le même premier mot");
    }
}
