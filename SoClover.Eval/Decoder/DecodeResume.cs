using SoClover.Domain;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Decoder;

/// <summary>
/// Boards restant à décoder. Depuis la phase 3, l'unité atomique de <c>decode</c> est le board
/// (spec §8 bis) : ses lignes sont écrites ensemble, après le checkpoint du traçage. Un board est
/// terminé si chaque direction à indice valide a ses <c>decodesPerClue</c> décodages et, quand les
/// quatre sont valides, sa ligne N3. Un board sans aucun indice valide n'écrit aucune ligne : il est
/// terminé si un board POSTÉRIEUR en a — le traitement est séquentiel.
/// </summary>
public static class DecodeResume
{
    public static IReadOnlyList<BenchBoard> PendingBoards(
        BenchContents bench,
        IReadOnlyDictionary<(string BoardId, string Direction), string> validClues,
        DecodeContents? existing,
        int decodesPerClue)
    {
        if (existing is null)
            return bench.Boards;

        var clueCounts = existing.ClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.Select(d => d.DecodeIndex).Distinct().Count());
        var boardDecoded = existing.BoardDecodes.Select(b => b.BoardId).ToHashSet(StringComparer.Ordinal);
        var boardsWithLines = clueCounts.Keys.Select(k => k.BoardId).Concat(boardDecoded).ToHashSet(StringComparer.Ordinal);
        var lastIndexWithLines = bench.Boards
            .Select((board, index) => (board, index))
            .Where(x => boardsWithLines.Contains(x.board.BoardId))
            .Select(x => x.index)
            .DefaultIfEmpty(-1)
            .Max();

        return bench.Boards.Where((board, index) => !IsComplete(board, index)).ToList();

        bool IsComplete(BenchBoard board, int index)
        {
            var valid = BoardGeometry.AllDirections
                .Where(d => validClues.ContainsKey((board.BoardId, d.ToString())))
                .ToList();
            if (valid.Count == 0)
                return index < lastIndexWithLines;

            var cluesDone = valid.All(d => clueCounts.GetValueOrDefault((board.BoardId, d.ToString())) >= decodesPerClue);
            var boardDone = valid.Count < 4 || boardDecoded.Contains(board.BoardId);
            return cluesDone && boardDone;
        }
    }
}
