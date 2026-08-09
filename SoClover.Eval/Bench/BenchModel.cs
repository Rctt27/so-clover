namespace SoClover.Eval.Bench;

/// <summary>Ligne 1 d'un fichier de banc : trace de reproductibilité.</summary>
public sealed record BenchManifest(
    string Kind,
    string BenchId,
    long Seed,
    int BoardCount,
    string Language,
    string DictionaryFile,
    string DictionaryHash,
    string PrngAlgorithm,
    int GeneratorVersion,
    DateTime CreatedAtUtc,
    string BenchHash);

/// <summary>Une arête et sa paire de référence, dérivée par <c>BoardGeometry.GetEdgeMapping</c>.</summary>
public sealed record BenchDirection(
    string Direction,
    IReadOnlyList<string> ReferenceWords);

/// <summary>
/// Stratification du board. <see cref="DrawDifficulty"/> reste <c>null</c> dans ce cycle :
/// le PRD le fait renseigner a posteriori par la séance A (P4). Le champ existe pour que
/// l'enrichissement ultérieur ne change pas de schéma.
/// </summary>
public sealed record BenchStrata(string? DrawDifficulty);

/// <summary>
/// Un board de banc. <see cref="Cards"/> est indexé par <c>BoardPosition</c>
/// (0=TopLeft, 1=TopRight, 2=BottomRight, 3=BottomLeft) et chaque carte par <c>Direction</c>
/// (0=Top, 1=Right, 2=Bottom, 3=Left) — l'orientation est toujours <c>Rotation.None</c>,
/// comme dans une partie réelle.
/// </summary>
public sealed record BenchBoard(
    string Kind,
    string BoardId,
    IReadOnlyList<IReadOnlyList<string>> Cards,
    IReadOnlyList<BenchDirection> Directions,
    BenchStrata Strata);

public sealed record BenchContents(
    BenchManifest Manifest,
    IReadOnlyList<BenchBoard> Boards);
