using SoClover.Domain;
using Xunit;

namespace SoClover.Tests.Domain;

public class BoardGeometryTests
{
    [Fact]
    public void AllDirections_is_the_canonical_clockwise_order()
    {
        Assert.Equal(
            new[] { Direction.Top, Direction.Right, Direction.Bottom, Direction.Left },
            BoardGeometry.AllDirections);
    }

    [Theory]
    [InlineData(Direction.Top,    BoardPosition.TopLeft,     Direction.Top,    BoardPosition.TopRight,    Direction.Top)]
    [InlineData(Direction.Right,  BoardPosition.TopRight,    Direction.Right,  BoardPosition.BottomRight, Direction.Right)]
    [InlineData(Direction.Bottom, BoardPosition.BottomRight, Direction.Bottom, BoardPosition.BottomLeft,  Direction.Bottom)]
    [InlineData(Direction.Left,   BoardPosition.BottomLeft,  Direction.Left,   BoardPosition.TopLeft,     Direction.Left)]
    public void GetEdgeMapping_returns_the_two_outer_faces_of_the_edge(
        Direction edge,
        BoardPosition expectedCardA, Direction expectedFaceA,
        BoardPosition expectedCardB, Direction expectedFaceB)
    {
        var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(edge);

        Assert.Equal(expectedCardA, cardA);
        Assert.Equal(expectedFaceA, faceA);
        Assert.Equal(expectedCardB, cardB);
        Assert.Equal(expectedFaceB, faceB);
    }

    [Fact]
    public void GetEdgeMapping_rejects_an_undefined_direction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoardGeometry.GetEdgeMapping((Direction)42));
    }

    // Cohérence avec CloverBoard.GetClueText : la carte A du mapping et sa face doivent désigner
    // exactement le mot que le domaine attend pour cet indice.
    [Theory]
    [InlineData(Direction.Top)]
    [InlineData(Direction.Right)]
    [InlineData(Direction.Bottom)]
    [InlineData(Direction.Left)]
    public void GetEdgeMapping_first_face_matches_CloverBoard_GetClueText(Direction edge)
    {
        var board = new CloverBoard();
        var words = new[]
        {
            new[] { "a1", "a2", "a3", "a4" },
            new[] { "b1", "b2", "b3", "b4" },
            new[] { "c1", "c2", "c3", "c4" },
            new[] { "d1", "d2", "d3", "d4" },
        };
        var positions = new[]
        {
            BoardPosition.TopLeft, BoardPosition.TopRight,
            BoardPosition.BottomRight, BoardPosition.BottomLeft,
        };
        for (var i = 0; i < 4; i++)
        {
            var card = new Card(CardId.New(), words[i][0], words[i][1], words[i][2], words[i][3]);
            board.Place(positions[i], new OrientedCard(card, Rotation.None));
        }

        var (cardA, faceA, _, _) = BoardGeometry.GetEdgeMapping(edge);

        Assert.Equal(words[(int)cardA][(int)faceA], board.GetClueText(edge));
    }
}
