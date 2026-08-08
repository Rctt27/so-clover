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

    /// <summary>
    /// Présentation <b>groupée par carte</b> (prompt clue v5). La partition en quatre cartes est
    /// une règle du jeu — dans une partie réelle le devineur tient les cartes physiques — mais la
    /// position de la carte sur le plateau et la face que porte chaque mot restent cachées : les
    /// cartes sont mélangées entre elles, puis les mots à l'intérieur de chacune.
    /// <para>
    /// Instance de PRNG <b>distincte</b> de celle de <see cref="Shuffle"/> : à graine égale, les
    /// deux rendus coexistent sans qu'aucun ne déplace l'autre, et les décodages déjà payés sous
    /// v2/v3/v4 restent reproductibles au bit près.
    /// </para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> ShuffleByCard(
        IReadOnlyList<IReadOnlyList<string>> cards, long seed)
    {
        var rng = new Xoshiro256SS(seed);
        var copy = cards.Select(card => card.ToList()).ToList();
        rng.Shuffle(copy);
        foreach (var card in copy)
            rng.Shuffle(card);
        return copy.Select(card => (IReadOnlyList<string>)card.AsReadOnly()).ToList().AsReadOnly();
    }

    /// <summary>
    /// Rend la présentation groupée. Les étiquettes sont des ordinaux neutres (« Carte 1 »…) :
    /// nommer une position ou une face ferait fuiter la géométrie que le décodeur doit ignorer.
    /// </summary>
    public static string RenderByCard(IReadOnlyList<IReadOnlyList<string>> cards) =>
        string.Join("\n\n", cards.Select((card, i) =>
            $"Carte {i + 1} :\n" + string.Join("\n", card.Select(w => $"- {w}"))));

    /// <summary>
    /// Rend les seize mots <b>à plat, chacun étiqueté par sa carte</b> (prompt clue v8).
    /// <para>
    /// L'ordre est celui de <paramref name="presentedFlat"/> — le mélange à plat de v4 — donc
    /// aucune adjacence intra-carte n'est créée : c'est tout l'objet de cette présentation. Les
    /// étiquettes sont tirées de <paramref name="shuffledCards"/> et non de l'ordre du board,
    /// sans quoi « carte A » désignerait toujours la première carte et la géométrie fuiterait par
    /// l'étiquette.
    /// </para>
    /// </summary>
    public static string RenderLabeled(
        IReadOnlyList<string> presentedFlat,
        IReadOnlyList<IReadOnlyList<string>> shuffledCards)
    {
        var labelOf = shuffledCards
            .SelectMany((card, index) => card.Select(word => (Word: word, Label: (char)('A' + index))))
            .ToDictionary(x => x.Word, x => x.Label, StringComparer.Ordinal);

        return string.Join("\n", presentedFlat.Select(w => $"- {w} (carte {labelOf[w]})"));
    }

    private static long Derive(string material)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return BitConverter.ToInt64(digest, 0);
    }
}
