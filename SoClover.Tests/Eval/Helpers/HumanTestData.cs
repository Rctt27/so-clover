using SoClover.Domain;
using SoClover.Eval.Bench;

namespace SoClover.Tests.Eval.Helpers;

/// <summary>
/// Fabriques partagées des suites P4-P5. Les bancs produits ici sont SYNTHÉTIQUES : mots
/// numérotés, sans radical commun, pour qu'aucun test ne dépende du dictionnaire réel — même
/// motif que DeterministicWordDictionary côté jeu.
/// </summary>
public static class HumanTestData
{
    public static BenchContents Bench(int boardCount = 10, string benchHash = "aaaaaaaaaaaa")
    {
        var boards = new List<BenchBoard>(boardCount);
        for (var b = 0; b < boardCount; b++)
        {
            var cards = new List<IReadOnlyList<string>>(4);
            for (var c = 0; c < 4; c++)
                cards.Add([$"mot{b}x{c}x0", $"mot{b}x{c}x1", $"mot{b}x{c}x2", $"mot{b}x{c}x3"]);

            var directions = BoardGeometry.AllDirections
                .Select(d => new BenchDirection(
                    d.ToString(),
                    BenchBoardMapperProbe.ReferenceWords(cards, d)))
                .ToList();

            boards.Add(new BenchBoard(
                Kind: "board",
                BoardId: $"dev-{b:D3}",
                Cards: cards.AsReadOnly(),
                Directions: directions.AsReadOnly(),
                Strata: new BenchStrata(null)));
        }

        var manifest = new BenchManifest(
            Kind: "manifest",
            BenchId: "dev",
            Seed: 1,
            BoardCount: boardCount,
            Language: "Français_OFF",
            DictionaryFile: "Français_OFF.txt",
            DictionaryHash: "bbbbbbbbbbbb",
            PrngAlgorithm: Xoshiro256SS.AlgorithmName,
            GeneratorVersion: 1,
            CreatedAtUtc: new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc),
            BenchHash: benchHash);

        return new BenchContents(manifest, boards.AsReadOnly());
    }

    /// <summary>
    /// Dérive la paire de référence par la même convention que le générateur de bancs.
    /// BenchBoardMapper.DeriveReferenceWords est `internal` au projet Eval ; le projet de test y
    /// a accès par InternalsVisibleTo, mais on passe par ce point unique pour que le jour où la
    /// convention bouge, un seul endroit des tests suive.
    /// </summary>
    private static class BenchBoardMapperProbe
    {
        public static IReadOnlyList<string> ReferenceWords(
            IReadOnlyList<IReadOnlyList<string>> cards, Direction edge)
        {
            var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(edge);
            return [cards[(int)cardA][(int)faceA], cards[(int)cardB][(int)faceB]];
        }
    }
}
