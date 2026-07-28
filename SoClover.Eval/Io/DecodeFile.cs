using System.Text;
using System.Text.Json;
using SoClover.Eval.Decoder;

namespace SoClover.Eval.Io;

/// <summary>
/// Fichier de décodage <c>&lt;runId&gt;.decoded.jsonl</c> : ligne 1 = manifeste décodeur,
/// lignes suivantes = <c>decode</c> (N2) et <c>boardDecode</c> (N3), discriminées par <c>kind</c>.
/// <para>
/// Fichier <b>distinct</b> du run générateur, jamais une réécriture en place : le run reste la
/// trace intacte de ce qu'a produit le modèle générateur.
/// </para>
/// </summary>
public static class DecodeFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string PathFor(string runPath)
    {
        var directory = Path.GetDirectoryName(runPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(runPath);
        return Path.Combine(directory, $"{name}.decoded.jsonl");
    }

    public static void WriteManifest(string path, DecodeManifest manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, EvalJson.Serialize(manifest) + "\n", Utf8NoBom);
    }

    public static void AppendClueDecode(string path, ClueDecodeLine line) =>
        File.AppendAllText(path, EvalJson.Serialize(line) + "\n", Utf8NoBom);

    public static void AppendBoardDecode(string path, BoardDecodeLine line) =>
        File.AppendAllText(path, EvalJson.Serialize(line) + "\n", Utf8NoBom);

    public static DecodeContents? ReadOrNull(string path) =>
        File.Exists(path) ? Read(path) : null;

    public static DecodeContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fichier de décodage introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new RunIntegrityException($"Fichier de décodage vide : {path}");

        DecodeManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<DecodeManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new RunIntegrityException($"Manifeste décodeur illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "manifest")
            throw new RunIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={manifest.Kind}).");

        if (manifest.HarnessVersion != RunFile.HarnessVersion)
            throw new RunIntegrityException(
                $"{path} : harnessVersion {manifest.HarnessVersion} incompatible avec {RunFile.HarnessVersion}.");

        var clueDecodes = new List<ClueDecodeLine>();
        var boardDecodes = new List<BoardDecodeLine>();

        for (var i = 1; i < lines.Count; i++)
        {
            try
            {
                using var doc = JsonDocument.Parse(lines[i]);
                var kind = doc.RootElement.GetProperty("kind").GetString();
                switch (kind)
                {
                    case "decode":
                        clueDecodes.Add(EvalJson.Deserialize<ClueDecodeLine>(lines[i]));
                        break;
                    case "boardDecode":
                        boardDecodes.Add(EvalJson.Deserialize<BoardDecodeLine>(lines[i]));
                        break;
                    default:
                        throw new RunIntegrityException(
                            $"{path} ligne {i + 1} : kind inconnu « {kind} ».");
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException && i == lines.Count - 1)
            {
                Console.Error.WriteLine(
                    $"AVERTISSEMENT : dernière ligne tronquée dans {path}, ignorée (décodage interrompu).");
            }
        }

        return new DecodeContents(manifest, clueDecodes.AsReadOnly(), boardDecodes.AsReadOnly());
    }
}
