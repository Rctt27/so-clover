using System.Text;
using System.Text.Json;
using SoClover.Eval.Calibration;
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

    private const string DecodedSuffix = ".decoded.jsonl";
    private const string MetricsSuffix = ".metrics.json";

    /// <summary>
    /// <c>&lt;runId&gt;.&lt;empreinte&gt;.decoded.jsonl</c>. L'empreinte fait partie du nom parce
    /// qu'un décodage <b>est</b> le produit d'un décodeur donné : deux décodeurs sur le même run
    /// sont deux artefacts, pas deux versions d'un seul.
    /// <para>
    /// Sans cela, la première variante essayée en P3 écrasait le décodage de référence — et avec
    /// lui le <c>.metrics.json</c> frère dont <c>calibrate</c> tire les portes de plancher et de
    /// non-saturation. Plusieurs centaines d'appels au LLM perdus en silence.
    /// </para>
    /// </summary>
    public static string PathFor(string runPath, string decoderFingerprint)
    {
        var directory = Path.GetDirectoryName(runPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(runPath);
        return Path.Combine(directory, $"{name}.{decoderFingerprint}{DecodedSuffix}");
    }

    /// <summary>
    /// Frère métriques du décodage. L'empreinte est ainsi portée par les deux noms, ce qui
    /// préserve la relation dont dépend <c>CalibrationGates.FingerprintOfMetrics</c> : le
    /// <c>.metrics.json</c> ne porte aucune empreinte dans son schéma, on la lit dans son frère.
    /// </summary>
    public static string MetricsPathFor(string decodedPath)
    {
        if (!decodedPath.EndsWith(DecodedSuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Chemin de décodage attendu en « {DecodedSuffix} », reçu : {decodedPath}",
                nameof(decodedPath));

        return decodedPath[..^DecodedSuffix.Length] + MetricsSuffix;
    }

    /// <summary>
    /// Run générateur dont provient un décodage — l'inverse de <see cref="PathFor"/>.
    /// <para>
    /// Le segment d'empreinte n'est retiré que s'il en a la forme exacte (12 caractères
    /// hexadécimaux). Un identifiant de run n'en contient jamais : les décodages hérités, sans
    /// empreinte, retombent donc naturellement sur le bon chemin.
    /// </para>
    /// </summary>
    public static string RunPathFor(string decodedPath)
    {
        if (!decodedPath.EndsWith(DecodedSuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Chemin de décodage attendu en « {DecodedSuffix} », reçu : {decodedPath}",
                nameof(decodedPath));

        var stem = decodedPath[..^DecodedSuffix.Length];
        var lastDot = stem.LastIndexOf('.');
        if (lastDot >= 0 && LooksLikeFingerprint(stem.AsSpan(lastDot + 1)))
            stem = stem[..lastDot];

        return stem + ".jsonl";
    }

    private static bool LooksLikeFingerprint(ReadOnlySpan<char> segment) =>
        segment.Length == DecoderFingerprint.HexLength
        && !segment.ContainsAnyExcept("0123456789abcdef");

    /// <summary>
    /// Le décodage d'un run quand on ne connaît pas l'empreinte — cas de <c>score</c>,
    /// <c>compare</c> et <c>analyze</c>, qui n'instancient aucun décodeur.
    /// <para>
    /// Rend <c>null</c> si aucun décodage n'existe, le chemin s'il n'y en a qu'un — <b>et refuse
    /// bruyamment dès qu'il y en a plusieurs</b>. C'est tout l'intérêt du correctif : une fois
    /// deux décodeurs en présence, aucun choix implicite n'est défendable, et un score publié
    /// sous le mauvais décodeur ne se voit sur aucun chiffre.
    /// </para>
    /// </summary>
    public static string? FindForRun(string runPath)
    {
        var directory = Path.GetDirectoryName(runPath);
        if (string.IsNullOrEmpty(directory)) directory = ".";
        if (!Directory.Exists(directory)) return null;

        var name = Path.GetFileNameWithoutExtension(runPath);

        // Le filtre porte sur le nom exact, jamais sur un préfixe : `run-1` ne doit pas ramasser
        // le décodage de `run-10`. Directory.EnumerateFiles avec un motif ne le garantit pas.
        var candidates = Directory
            .EnumerateFiles(directory, $"*{DecodedSuffix}")
            .Where(p => IsDecodeOf(Path.GetFileName(p), name))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"{candidates.Count} décodages coexistent pour {name} — un par décodeur. " +
                "Aucun choix implicite n'est défendable : désigner celui qu'on veut avec " +
                "--decoded <chemin>." + Environment.NewLine +
                string.Join(Environment.NewLine, candidates.Select(p => $"  {Path.GetFileName(p)}"))),
        };
    }

    /// <summary>
    /// <c>&lt;name&gt;.&lt;empreinte&gt;.decoded.jsonl</c>, ou <c>&lt;name&gt;.decoded.jsonl</c>
    /// pour les décodages antérieurs à la convention — trop coûteux à reproduire pour être rendus
    /// invisibles par un renommage.
    /// </summary>
    private static bool IsDecodeOf(string fileName, string runName)
    {
        var stem = fileName[..^DecodedSuffix.Length];
        if (string.Equals(stem, runName, StringComparison.Ordinal)) return true;

        return stem.Length > runName.Length
               && stem.StartsWith(runName, StringComparison.Ordinal)
               && stem[runName.Length] == '.'
               && !stem.AsSpan(runName.Length + 1).Contains('.');
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
