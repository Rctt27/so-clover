using SoClover.Eval.Bench;
using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

/// <summary>
/// Ce qu'un item du kit embarque : exactement le contenu de <see cref="GuessItemView"/>, et rien
/// de plus. <b>Ni <c>boardId</c>, ni paire de référence</b> — la page hors ligne ne peut pas
/// révéler ce qu'elle ne contient pas, et le <c>r</c> reste calculé à l'import, sur la machine de
/// l'opérateur.
/// </summary>
public sealed record GuessKitItem(IReadOnlyList<string> PresentedWords, string Clue);

/// <summary>
/// Charge utile gravée dans le kit. Les quatre champs d'identité (<c>benchHash</c>, <c>seed</c>,
/// <c>runId</c>, <c>kitHash</c>) reviennent dans le fichier de résultat : c'est par eux que
/// <see cref="GuessKitImport"/> refuse un montage qui aurait divergé entre l'envoi et le retour.
/// </summary>
public sealed record GuessKitPayload(
    string Kind,
    string BenchHash,
    long Seed,
    string RunId,
    string KitHash,
    int HarnessVersion,
    int ItemCount,
    DateTime CreatedAtUtc,
    IReadOnlyList<GuessKitItem> Items);

/// <summary>
/// Séance E hors ligne. Le kit est un <b>transport</b>, jamais un second protocole : il grave la
/// présentation exacte de la séance D dans un fichier HTML autonome, que H2 ouvre sans serveur et
/// sans réseau, et son résultat se réinjecte au format d'une séance servie
/// (<see cref="GuessKitImport"/>).
/// <para>
/// Ce que le serveur garantissait et que le hors-ligne ne peut pas garantir : le plan entier est
/// dans la page, donc un devineur qui ouvrirait les outils de développement verrait les items à
/// venir (jamais les réponses). C'est la concession déclarée au registre — elle ne se répare pas,
/// elle se consigne.
/// </para>
/// </summary>
public static class GuessKit
{
    public const string PayloadKind = "guess-kit";

    /// <summary>Longueur du <c>kitHash</c>, alignée sur les empreintes courtes du harnais.</summary>
    public const int KitHashHexLength = 12;

    public static GuessKitPayload BuildPayload(
        BenchContents bench,
        IReadOnlyList<GuessPlanItem> plan,
        GuessingManifest manifest,
        DateTime createdAtUtc)
    {
        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        // GuessingSession.PresentedWords est le point d'entrée unique : le kit ne recalcule pas
        // l'ordre de présentation, il rappelle celui que le serveur sert.
        var items = plan
            .Select(item => new GuessKitItem(
                GuessingSession.PresentedWords(boards[item.BoardId], bench.Manifest.BenchHash),
                item.Clue))
            .ToList()
            .AsReadOnly();

        return new GuessKitPayload(
            Kind: PayloadKind,
            BenchHash: bench.Manifest.BenchHash,
            Seed: manifest.Seed,
            RunId: manifest.RunId,
            KitHash: EvalJson.ComputeItemsHash(items, KitHashHexLength),
            HarnessVersion: HumanFile.HarnessVersion,
            ItemCount: items.Count,
            CreatedAtUtc: createdAtUtc,
            Items: items);
    }
}
