using SoClover.Domain;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Bench;

/// <summary>
/// Pont entre la représentation sérialisée d'un board de banc et les types du domaine.
/// Un board de banc est toujours en <c>Rotation.None</c> — c'est ce que fait
/// <c>StartWritingPhase</c>, et le pipeline IA ne tourne jamais les cartes.
/// </summary>
public static class BenchBoardMapper
{
    private static readonly BoardPosition[] Positions =
        [BoardPosition.TopLeft, BoardPosition.TopRight, BoardPosition.BottomRight, BoardPosition.BottomLeft];

    public static IReadOnlyList<BoardCardSnapshot> ToSnapshots(BenchBoard board)
    {
        var snapshots = new List<BoardCardSnapshot>(4);
        for (var i = 0; i < 4; i++)
        {
            var w = board.Cards[i];
            snapshots.Add(new BoardCardSnapshot(Positions[i], w[0], w[1], w[2], w[3]));
        }
        return snapshots.AsReadOnly();
    }

    public static CloverBoard ToCloverBoard(BenchBoard board)
    {
        var clover = new CloverBoard();
        for (var i = 0; i < 4; i++)
        {
            var w = board.Cards[i];
            var card = new Card(CardId.New(), w[0], w[1], w[2], w[3]);
            clover.Place(Positions[i], new OrientedCard(card, Rotation.None));
        }
        return clover;
    }

    /// <summary>Les 16 mots, dans l'ordre du board (jamais l'ordre présenté au décodeur).</summary>
    public static IReadOnlyList<string> AllWords(BenchBoard board) =>
        board.Cards.SelectMany(c => c).ToList().AsReadOnly();

    public static IReadOnlyList<string> ReferenceWords(BenchBoard board, Direction direction) =>
        board.Directions.Single(d => d.Direction == direction.ToString()).ReferenceWords;

    public static IReadOnlyList<string> ReferenceWords(
        IReadOnlyList<IReadOnlyList<string>> cards, Direction edge)
    {
        var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(edge);
        return [cards[(int)cardA][(int)faceA], cards[(int)cardB][(int)faceB]];
    }
}
