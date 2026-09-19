using System.Text.Json.Nodes;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Banc → dataset Langfuse, <b>un item par direction</b> (spec §4, D3) : c'est l'unité de <c>r</c>,
/// du Δ apparié et des séances humaines.
/// </summary>
public static class BenchDatasetMapper
{
    // Indexation de BenchBoard.Cards : 0=TopLeft, 1=TopRight, 2=BottomRight, 3=BottomLeft.
    private static readonly string[] Positions = ["TopLeft", "TopRight", "BottomRight", "BottomLeft"];

    public static string DatasetName(BenchManifest manifest) => $"soclover-bench-{manifest.BenchId}";

    /// <summary>
    /// Langfuse impose l'unicité d'un id de dataset-item <b>par projet, tous datasets confondus</b>
    /// — pas seulement au sein d'un dataset. La propriété tient aujourd'hui parce que chaque
    /// <c>boardId</c> est déjà préfixé par le <c>benchId</c> (ex. <c>dev-001</c>, <c>test-001</c>) :
    /// deux bancs distincts ne peuvent donc pas produire le même id. Incident constaté le
    /// 2026-09-17 : un item de <c>spike-dataset</c> (résidu d'un essai manuel, id <c>dev-001-Top</c>)
    /// a bloqué la création de <c>soclover-bench-dev</c> par conflit d'id au niveau du projet.
    /// Tout futur dataset publié dans ce projet Langfuse doit préserver cette propriété de préfixage
    /// — ne pas changer ce format d'id sans revérifier l'unicité globale.
    /// </summary>
    public static string ItemId(string boardId, string direction) => $"{boardId}-{direction}";

    public static bool IsTestBench(BenchManifest manifest) =>
        string.Equals(manifest.BenchId, "test", StringComparison.OrdinalIgnoreCase);

    public static void RequireNotTestBench(BenchManifest manifest)
    {
        if (IsTestBench(manifest))
            throw new InvalidOperationException(
                "Le banc de test ne se pousse pas dans Langfuse (spec §7.1) : le consulter est un acte de " +
                "jalon consigné au registre, pas une page qu'on ouvre.");
    }

    public static JsonObject Input(BenchBoard board, string direction) => new()
    {
        ["boardId"] = board.BoardId,
        ["direction"] = direction,
        // Faces indexées par Direction : 0=Top, 1=Right, 2=Bottom, 3=Left.
        ["cards"] = new JsonArray(board.Cards.Select((faces, i) => (JsonNode?)new JsonObject
        {
            ["position"] = Positions[i],
            ["top"] = faces[0],
            ["right"] = faces[1],
            ["bottom"] = faces[2],
            ["left"] = faces[3],
        }).ToArray()),
        ["words"] = new JsonArray(BenchBoardMapper.AllWords(board).Select(w => (JsonNode?)w).ToArray()),
    };

    public static IReadOnlyList<LangfuseDatasetItem> ToItems(BenchContents bench) =>
        bench.Boards
            .SelectMany(board => board.Directions.Select(direction => new LangfuseDatasetItem(
                ItemId(board.BoardId, direction.Direction),
                Input(board, direction.Direction),
                new JsonObject
                {
                    ["referenceWords"] = new JsonArray(direction.ReferenceWords.Select(w => (JsonNode?)w).ToArray()),
                },
                new JsonObject
                {
                    ["benchId"] = bench.Manifest.BenchId,
                    ["benchHash"] = bench.Manifest.BenchHash,
                    ["boardId"] = board.BoardId,
                    ["direction"] = direction.Direction,
                })))
            .ToList();
}
