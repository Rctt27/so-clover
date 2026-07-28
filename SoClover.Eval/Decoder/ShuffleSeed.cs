using System.Security.Cryptography;
using System.Text;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Decoder;

/// <summary>
/// Ordre de présentation des 16 mots au décodeur : fonction déterministe de
/// <c>(benchHash, boardId, decodeIndex)</c>, jamais l'ordre du board.
/// <para>
/// Les 3 décodages d'un même indice diffèrent donc à la fois par l'échantillonnage du modèle
/// <b>et</b> par l'ordre de présentation — ce qui neutralise au passage le biais de position,
/// gratuitement.
/// </para>
/// </summary>
public static class ShuffleSeed
{
    public static long ForClue(string benchHash, string boardId, int decodeIndex) =>
        Derive($"{benchHash}|{boardId}|{decodeIndex}");

    public static long ForBoard(string benchHash, string boardId) =>
        Derive($"{benchHash}|{boardId}|board");

    public static IReadOnlyList<string> Shuffle(IReadOnlyList<string> words, long seed)
    {
        var copy = words.ToList();
        new Xoshiro256SS(seed).Shuffle(copy);
        return copy.AsReadOnly();
    }

    private static long Derive(string material)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return BitConverter.ToInt64(digest, 0);
    }
}
