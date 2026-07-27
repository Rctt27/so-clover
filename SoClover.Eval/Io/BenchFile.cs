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
    public static string ComputeBenchHash(IReadOnlyList<BenchBoard> boards) =>
        EvalJson.ComputeItemsHash(boards, BenchGenerator.BenchHashHexLength);

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
