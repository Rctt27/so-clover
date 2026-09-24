using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;

namespace SoClover.Tests.Eval.Helpers;

internal static class DecodeFixtures
{
    /// <summary>Banc à deux boards : <see cref="LangfuseFixtures.Bench"/> n'en a qu'un.</summary>
    public static BenchContents TwoBoardBench() =>
        BenchGenerator.Generate(
            "dev", 20260726001, 2,
            ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
             "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont",
             "Paradis", "Membre", "Vêtement", "Chêne", "Terrasse", "Tarte", "Déchet", "Voleur",
             "Maître", "Herbe", "Liquide", "Fable", "Miroir", "Fuite", "Collier", "Ampoule"],
            "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc));

    public static DecodeContents Complete(BenchBoard board, int decodesPerClue, bool withBoardDecode) => new(
        LangfuseFixtures.DecodeManifest(),
        board.Directions
            .SelectMany(d => Enumerable.Range(0, decodesPerClue).Select(i =>
                new ClueDecodeLine("clueDecode", board.BoardId, d.Direction, i, d.ReferenceWords, 1.0, "seed", null, 10)))
            .ToList(),
        withBoardDecode
            ? [new BoardDecodeLine("boardDecode", board.BoardId, null, 1.0, true, "seed", null, 10)]
            : []);
}
