using System.Text;
using System.Text.Json;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Io;

/// <summary>Le banc lu ne correspond pas à ce qu'il déclare être.</summary>
// Brief : dérive de System.IO.InvalidDataException. En .NET 9, ce type est `sealed`
// (CS0509) — dérivation impossible. Exception est la base la plus proche sans changer
// le contrat observable (tests : Assert.Throws<BenchIntegrityException>, ex.Message).
public sealed class BenchIntegrityException : Exception
{
    public BenchIntegrityException(string message) : base(message) { }
}

/// <summary>
/// Lecture / écriture d'un fichier de banc JSONL : ligne 1 = manifeste, lignes suivantes = boards.
/// La lecture <b>refuse</b> un banc dont le hash recalculé diffère du hash déclaré — un banc qui
/// bouge invalide tout l'historique du registre, ce n'est pas un avertissement.
/// </summary>
public static class BenchFile
{
    // Point d'entrée UNIQUE pour le calcul du benchHash : écrivain (BenchGenerator, au moment de
    // produire le manifeste) et lecteur (Read, ci-dessous, au moment de le revalider) doivent
    // impérativement partager cette méthode. Un doublon (chacun recalculant sa propre projection)
    // désynchroniserait silencieusement politique d'écriture et politique de lecture : modifier
    // l'une sans l'autre produirait un manifeste que l'autre refuse.
    public static string ComputeBenchHash(IReadOnlyList<BenchBoard> boards) =>
        EvalJson.ComputeItemsHash(boards.Select(FrozenOracle), BenchGenerator.BenchHashHexLength);

    // Projection hachée = l'oracle immuable par contrat d'un board : boardId, cards, directions
    // (donc les paires de référence). `strata` en est délibérément exclu : le PRD prévoit que la
    // séance A (P4) remplira `strata.drawDifficulty` EN PLACE dans les deux bancs déjà committés
    // et gelés — ce remplissage ne doit PAS faire dériver benchHash ni invalider l'historique du
    // registre. `strata` reste écrit dans le fichier (Write sérialise le BenchBoard complet, tel
    // quel) : il sort seulement du périmètre haché, pas du schéma ni du fichier.
    private static object FrozenOracle(BenchBoard board) =>
        new { board.BoardId, board.Cards, board.Directions };

    public static void Write(string path, BenchContents contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var sb = new StringBuilder();
        sb.Append(EvalJson.Serialize(contents.Manifest)).Append('\n');
        foreach (var board in contents.Boards)
            sb.Append(EvalJson.Serialize(board)).Append('\n');

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static BenchContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Banc introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new BenchIntegrityException($"Banc vide : {path}");

        BenchManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<BenchManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new BenchIntegrityException($"Manifeste illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "manifest")
            throw new BenchIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={manifest.Kind}).");

        var boards = new List<BenchBoard>(lines.Count - 1);
        for (var i = 1; i < lines.Count; i++)
        {
            try
            {
                boards.Add(EvalJson.Deserialize<BenchBoard>(lines[i]));
            }
            catch (JsonException ex)
            {
                throw new BenchIntegrityException($"Board illisible ligne {i + 1} de {path} : {ex.Message}");
            }
        }

        if (boards.Count != manifest.BoardCount)
            throw new BenchIntegrityException(
                $"{path} : le manifeste annonce {manifest.BoardCount} boards, le fichier en contient {boards.Count}.");

        var recomputed = ComputeBenchHash(boards);
        if (recomputed != manifest.BenchHash)
            throw new BenchIntegrityException(
                $"{path} : benchHash déclaré {manifest.BenchHash}, recalculé {recomputed}. " +
                "Le banc a dérivé — l'historique du registre serait invalidé.");

        return new BenchContents(manifest, boards.AsReadOnly());
    }
}
