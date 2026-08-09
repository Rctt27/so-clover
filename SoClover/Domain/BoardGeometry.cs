namespace SoClover.Domain;

/// <summary>
/// Convention « faces extérieures » du plateau 2x2, définie une seule fois.
/// Chaque indice évoque les deux mots des cartes bordant son côté, sur les faces
/// visuellement adjacentes à l'indice (celles qui pointent vers le bord extérieur).
/// Consommée par le rendu de prompt (<c>FileAiCluePromptProvider</c>) et par le
/// générateur de banc du harnais d'évaluation — d'où sa place dans le Domain,
/// sans aucune dépendance Infrastructure.
/// </summary>
public static class BoardGeometry
{
    private static readonly Direction[] Clockwise =
        [Direction.Top, Direction.Right, Direction.Bottom, Direction.Left];

    /// <summary>Ordre canonique des quatre arêtes, sens horaire depuis le haut.</summary>
    public static IReadOnlyList<Direction> AllDirections { get; } = Array.AsReadOnly(Clockwise);

    /// <summary>
    /// Pour une arête, les deux (carte, face) dont les mots sont ceux que l'indice doit évoquer.
    /// </summary>
    public static (BoardPosition CardA, Direction FaceA, BoardPosition CardB, Direction FaceB)
        GetEdgeMapping(Direction edge)
        => edge switch
        {
            Direction.Top    => (BoardPosition.TopLeft,     Direction.Top,    BoardPosition.TopRight,    Direction.Top),
            Direction.Right  => (BoardPosition.TopRight,    Direction.Right,  BoardPosition.BottomRight, Direction.Right),
            Direction.Bottom => (BoardPosition.BottomRight, Direction.Bottom, BoardPosition.BottomLeft,  Direction.Bottom),
            Direction.Left   => (BoardPosition.BottomLeft,  Direction.Left,   BoardPosition.TopLeft,     Direction.Left),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };
}
