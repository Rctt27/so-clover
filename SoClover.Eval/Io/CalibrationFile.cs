using System.Text;
using System.Text.Json;
using SoClover.Eval.Calibration;

namespace SoClover.Eval.Io;

/// <summary>
/// Fichier de calibration <c>calibration.&lt;date&gt;-&lt;empreinte&gt;.jsonl</c> : ligne 1 =
/// manifeste, lignes suivantes = <c>calibrationDecode</c>. Append-only, reprenable, jamais
/// réécrit — même contrat que <see cref="RunFile"/> et <see cref="DecodeFile"/>.
/// <para>
/// Le fichier est <b>committé</b> : avec les deux corpus humains, il est l'investissement
/// irremplaçable du chantier. Les tentatives successives s'accumulent, elles ne s'écrasent pas —
/// c'est ce qui rendra lisible, dans six mois, l'effet du passage de <c>decode-clue</c> v1 à v2.
/// </para>
/// </summary>
public static class CalibrationFile
{
    public const int HarnessVersion = 1;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string PathFor(string directory, string calibrationId) =>
        Path.Combine(directory, $"calibration.{calibrationId}.jsonl");

    /// <summary>Le rapport frère : même nom, extension <c>.json</c>.</summary>
    public static string ReportPathFor(string jsonlPath) =>
        Path.ChangeExtension(jsonlPath, ".json");

    public static void WriteManifest(string path, CalibrationManifest manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, EvalJson.Serialize(manifest) + "\n", Utf8NoBom);
    }

    public static void AppendDecode(string path, CalibrationDecode decode) =>
        File.AppendAllText(path, EvalJson.Serialize(decode) + "\n", Utf8NoBom);

    public static CalibrationContents? ReadOrNull(string path) =>
        File.Exists(path) ? Read(path) : null;

    public static CalibrationContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fichier de calibration introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new RunIntegrityException($"Fichier de calibration vide : {path}");

        CalibrationManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<CalibrationManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new RunIntegrityException($"Manifeste de calibration illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "manifest")
            throw new RunIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={manifest.Kind}).");

        if (manifest.HarnessVersion != HarnessVersion)
            throw new RunIntegrityException(
                $"{path} : harnessVersion {manifest.HarnessVersion} incompatible avec {HarnessVersion}.");

        var decodes = new List<CalibrationDecode>();
        for (var i = 1; i < lines.Count; i++)
        {
            try
            {
                using var doc = JsonDocument.Parse(lines[i]);
                var kind = doc.RootElement.GetProperty("kind").GetString();
                if (kind != CalibrationDecode.LineKind)
                    throw new RunIntegrityException(
                        $"{path} ligne {i + 1} : kind inconnu « {kind} ».");

                decodes.Add(EvalJson.Deserialize<CalibrationDecode>(lines[i]));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException && i == lines.Count - 1)
            {
                Console.Error.WriteLine(
                    $"AVERTISSEMENT : dernière ligne tronquée dans {path}, ignorée (calibration interrompue). " +
                    "La reprise la regénérera.");
            }
        }

        return new CalibrationContents(manifest, decodes.AsReadOnly());
    }
}
