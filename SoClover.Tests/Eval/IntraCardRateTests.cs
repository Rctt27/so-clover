using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Taux de paires <b>intra-carte</b> : la part des décodages exploitables dont les deux mots
/// viennent d'une seule et même carte. Une direction est une arête entre deux cartes, donc la
/// paire de référence vient <b>toujours</b> de deux cartes distinctes — une paire intra-carte
/// ne peut jamais valoir <c>r = 1</c>. C'est une mesure de conformité à une règle du jeu, pas
/// une porte : le code ne rejette rien, il compte.
/// </summary>
public class IntraCardRateTests
{
    private static DecodeContents Decoded(BenchContents bench, params ClueDecodeLine[] lines) =>
        new(
            new DecodeManifest(
                "manifest", "decode-1", DateTime.UtcNow, "run-a",
                "eval/boards.dev.jsonl", bench.Manifest.BenchHash, "test", "", "test", null, null,
                0.3, 1.0, 512, "decode-clue.md", 5, "decode-board.md", 1, 3, 1, null),
            lines.ToList().AsReadOnly(),
            new List<BoardDecodeLine>().AsReadOnly());

    private static ClueDecodeLine Pick(
        BenchBoard board, Direction direction, int index, string first, string second) =>
        new("decode", board.BoardId, direction.ToString(), index, [first, second], 0.0, "0", null, 5);

    [Fact]
    public void Counts_a_pair_taken_from_a_single_card_as_intra_card()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var board = bench.Boards[0];
        var decoded = Decoded(bench, Pick(board, Direction.Top, 0, board.Cards[0][0], board.Cards[0][1]));

        var m = RunMetrics.Compute(bench, HumanTestData.Run(bench, "run-a", "modelA"), decoded, 1);

        Assert.Equal(1.0, m.IntraCardRate);
        Assert.Equal(1, m.Counts.IntraCardPicks);
        Assert.Equal(1, m.Counts.ScoredPicks);
    }

    [Fact]
    public void Does_not_count_a_pair_spanning_two_cards()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var board = bench.Boards[0];
        var decoded = Decoded(bench, Pick(board, Direction.Top, 0, board.Cards[0][0], board.Cards[1][0]));

        var m = RunMetrics.Compute(bench, HumanTestData.Run(bench, "run-a", "modelA"), decoded, 1);

        Assert.Equal(0.0, m.IntraCardRate);
        Assert.Equal(0, m.Counts.IntraCardPicks);
        Assert.Equal(1, m.Counts.ScoredPicks);
    }

    [Fact]
    public void Averages_over_every_exploitable_decode()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var board = bench.Boards[0];
        var decoded = Decoded(
            bench,
            Pick(board, Direction.Top, 0, board.Cards[0][0], board.Cards[0][1]),
            Pick(board, Direction.Top, 1, board.Cards[0][0], board.Cards[1][0]),
            Pick(board, Direction.Top, 2, board.Cards[2][0], board.Cards[3][0]),
            Pick(board, Direction.Right, 0, board.Cards[3][1], board.Cards[3][2]));

        var m = RunMetrics.Compute(bench, HumanTestData.Run(bench, "run-a", "modelA"), decoded, 1);

        Assert.Equal(0.5, m.IntraCardRate);
        Assert.Equal(2, m.Counts.IntraCardPicks);
        Assert.Equal(4, m.Counts.ScoredPicks);
    }

    // Même règle que R̄ : un décodage sans réponse exploitable n'est pas un décodage qui viole
    // la contrainte, il sort des deux termes du rapport.
    [Fact]
    public void Excludes_format_failures_from_both_terms()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var board = bench.Boards[0];
        var decoded = Decoded(
            bench,
            Pick(board, Direction.Top, 0, board.Cards[0][0], board.Cards[0][1]),
            new ClueDecodeLine("decode", board.BoardId, "Top", 1, null, null, "0", "outOfVocabulary", 5));

        var m = RunMetrics.Compute(bench, HumanTestData.Run(bench, "run-a", "modelA"), decoded, 1);

        Assert.Equal(1.0, m.IntraCardRate);
        Assert.Equal(1, m.Counts.ScoredPicks);
    }

    // Dénominateur nul : pas de taux fabriqué, et l'effectif le dit.
    [Fact]
    public void Reports_no_rate_when_nothing_was_decoded()
    {
        var bench = HumanTestData.Bench(boardCount: 1);

        var m = RunMetrics.Compute(bench, HumanTestData.Run(bench, "run-a", "modelA"), Decoded(bench), 1);

        Assert.Equal(0.0, m.IntraCardRate);
        Assert.Equal(0, m.Counts.ScoredPicks);
    }

    // Le sous-ensemble restreint tous les dénominateurs, celui-ci compris.
    [Fact]
    public void Follows_the_subset_restriction()
    {
        var bench = HumanTestData.Bench(boardCount: 1);
        var board = bench.Boards[0];
        var decoded = Decoded(
            bench,
            Pick(board, Direction.Top, 0, board.Cards[0][0], board.Cards[0][1]),
            Pick(board, Direction.Right, 0, board.Cards[0][0], board.Cards[1][0]));

        var m = RunMetrics.Compute(
            bench, HumanTestData.Run(bench, "run-a", "modelA"), decoded, 1,
            subset: new HashSet<(string BoardId, string Direction)> { (board.BoardId, "Top") });

        Assert.Equal(1.0, m.IntraCardRate);
        Assert.Equal(1, m.Counts.ScoredPicks);
    }
}
