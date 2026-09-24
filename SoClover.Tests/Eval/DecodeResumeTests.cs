using SoClover.Eval.Decoder;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class DecodeResumeTests
{
    // LangfuseFixtures.Bench() n'a qu'un board : le dernier test en exige deux.
    private static readonly SoClover.Eval.Bench.BenchContents Bench = DecodeFixtures.TwoBoardBench();

    private static Dictionary<(string, string), string> AllValid() =>
        Bench.Boards.SelectMany(b => b.Directions.Select(d => ((b.BoardId, d.Direction), "Indice"))).ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public void Without_an_existing_file_every_board_is_pending() =>
        Assert.Equal(Bench.Boards.Count, DecodeResume.PendingBoards(Bench, AllValid(), null, 3).Count);

    [Fact]
    public void A_board_is_done_when_all_its_valid_directions_have_their_decodes_and_its_board_decode()
    {
        var board = Bench.Boards[0];
        var existing = DecodeFixtures.Complete(board, decodesPerClue: 3, withBoardDecode: true);
        var pending = DecodeResume.PendingBoards(Bench, AllValid(), existing, 3);
        Assert.DoesNotContain(board, pending);
        Assert.Contains(Bench.Boards[1], pending);
    }

    [Fact]
    public void A_board_missing_its_board_decode_is_pending_when_its_four_clues_are_valid()
    {
        var board = Bench.Boards[0];
        var existing = DecodeFixtures.Complete(board, decodesPerClue: 3, withBoardDecode: false);
        Assert.Contains(board, DecodeResume.PendingBoards(Bench, AllValid(), existing, 3));
    }

    [Fact]
    public void A_board_missing_a_clue_decode_is_pending()
    {
        var board = Bench.Boards[0];
        var existing = DecodeFixtures.Complete(board, decodesPerClue: 2, withBoardDecode: true);
        Assert.Contains(board, DecodeResume.PendingBoards(Bench, AllValid(), existing, 3));
    }

    [Fact]
    public void A_board_without_any_valid_clue_is_done_only_if_a_later_board_has_lines()
    {
        // Board 0 entièrement A-1, board 1 décodé : le board 0 a été traité avant lui.
        var validOnlyLater = AllValid().Where(kv => kv.Key.Item1 != Bench.Boards[0].BoardId).ToDictionary();
        var existing = DecodeFixtures.Complete(Bench.Boards[1], decodesPerClue: 3, withBoardDecode: true);
        var pending = DecodeResume.PendingBoards(Bench, validOnlyLater, existing, 3);
        Assert.DoesNotContain(Bench.Boards[0], pending);
        Assert.DoesNotContain(Bench.Boards[1], pending);
    }

    [Fact]
    public void A_last_board_without_any_valid_clue_stays_pending()
    {
        // Aucun board postérieur ne prouve que le dernier a été traité : il est refait (sans coût, aucune ligne).
        var validOnlyFirst = AllValid().Where(kv => kv.Key.Item1 == Bench.Boards[0].BoardId).ToDictionary();
        var existing = DecodeFixtures.Complete(Bench.Boards[0], decodesPerClue: 3, withBoardDecode: true);
        var pending = DecodeResume.PendingBoards(Bench, validOnlyFirst, existing, 3);
        Assert.Equal(new[] { Bench.Boards[1] }, pending);
    }
}
