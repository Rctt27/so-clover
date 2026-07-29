using System.Text;
using System.Text.Json;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;

namespace SoClover.Eval.Io;

/// <summary>Le corpus humain lu ne correspond pas à ce qu'il déclare être.</summary>
// Même arbitrage que BenchIntegrityException et RunIntegrityException : InvalidDataException
// est `sealed` en .NET 9, la dérivation est impossible.
public sealed class HumanIntegrityException : Exception
{
    public HumanIntegrityException(string message) : base(message) { }
}

/// <summary>
/// Fichiers JSONL des séances humaines : ligne 1 = manifeste, lignes suivantes = un événement
/// chacune, <b>jamais réécrites</b> — même religion que <see cref="RunFile"/>, où une ligne vaut
/// une tentative et où la résolution est dérivée par le lecteur.
/// </summary>
public static class HumanFile
{
    public const int HarnessVersion = 1;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // ── Écriture ────────────────────────────────────────────────────────────

    public static void WriteElicitationManifest(string path, ElicitationManifest manifest) =>
        WriteManifestLine(path, manifest);

    public static void AppendElicitation(string path, ElicitationLine line) => AppendLine(path, line);

    public static void AppendAssist(string path, AssistLine line) => AppendLine(path, line);

    public static void WriteComparisonManifest(string path, ComparisonManifest manifest) =>
        WriteManifestLine(path, manifest);

    public static void AppendComparison(string path, ComparisonLine line) => AppendLine(path, line);

    private static void WriteManifestLine<T>(string path, T manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, EvalJson.Serialize(manifest) + "\n", Utf8);
    }

    private static void AppendLine<T>(string path, T line) =>
        File.AppendAllText(path, EvalJson.Serialize(line) + "\n", Utf8);

    // ── Lecture ─────────────────────────────────────────────────────────────

    public static ElicitationContents? ReadElicitationOrNull(string path) =>
        File.Exists(path) ? ReadElicitation(path) : null;

    public static ElicitationContents ReadElicitation(string path)
    {
        var lines = ReadNonEmptyLines(path);
        var manifest = ReadManifest<ElicitationManifest>(path, lines[0], m => m.Kind, m => m.HarnessVersion);

        var elicitations = new List<ElicitationLine>();
        var assists = new List<AssistLine>();

        for (var i = 1; i < lines.Count; i++)
        {
            var isLast = i == lines.Count - 1;
            try
            {
                switch (PeekKind(lines[i]))
                {
                    case "elicitation":
                        elicitations.Add(EvalJson.Deserialize<ElicitationLine>(lines[i]));
                        break;
                    case "assist":
                        assists.Add(EvalJson.Deserialize<AssistLine>(lines[i]));
                        break;
                    case var kind:
                        throw new HumanIntegrityException(
                            $"{path} ligne {i + 1} : kind inconnu « {kind} ».");
                }
            }
            catch (JsonException) when (isLast)
            {
                WarnTruncated(path);
            }
        }

        return new ElicitationContents(manifest, elicitations.AsReadOnly(), assists.AsReadOnly());
    }

    public static ComparisonContents? ReadComparisonsOrNull(string path) =>
        File.Exists(path) ? ReadComparisons(path) : null;

    public static ComparisonContents ReadComparisons(string path)
    {
        var lines = ReadNonEmptyLines(path);
        var manifest = ReadManifest<ComparisonManifest>(path, lines[0], m => m.Kind, m => m.HarnessVersion);

        var comparisons = new List<ComparisonLine>();
        for (var i = 1; i < lines.Count; i++)
        {
            var isLast = i == lines.Count - 1;
            try
            {
                if (PeekKind(lines[i]) is not "comparison")
                    throw new HumanIntegrityException(
                        $"{path} ligne {i + 1} : kind inconnu « {PeekKind(lines[i])} ».");

                comparisons.Add(EvalJson.Deserialize<ComparisonLine>(lines[i]));
            }
            catch (JsonException) when (isLast)
            {
                WarnTruncated(path);
            }
        }

        return new ComparisonContents(manifest, comparisons.AsReadOnly());
    }

    // ── Dérivations ─────────────────────────────────────────────────────────

    /// <summary>
    /// Dernier verdict par <c>comparisonId</c>. Un re-jugement ajoute une ligne, il n'en réécrit
    /// jamais une : c'est le lecteur qui retient la dernière.
    /// </summary>
    public static IReadOnlyDictionary<string, ComparisonLine> LatestByComparisonId(ComparisonContents contents) =>
        contents.Comparisons
            .GroupBy(c => c.ComparisonId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    public static AssistLine? AssistFor(ElicitationContents contents, string boardId, string direction) =>
        contents.Assists.LastOrDefault(a =>
            string.Equals(a.BoardId, boardId, StringComparison.Ordinal) &&
            string.Equals(a.Direction, direction, StringComparison.Ordinal));

    /// <summary>
    /// Refus de démarrer si le manifeste porte un autre banc que celui relu : une séance reprise
    /// sur un banc qui a bougé produirait des paires cibles fausses sans le signaler.
    /// </summary>
    public static void RequireBench(string path, string manifestBenchHash, BenchContents bench)
    {
        if (!string.Equals(manifestBenchHash, bench.Manifest.BenchHash, StringComparison.Ordinal))
            throw new HumanIntegrityException(
                $"{path} déclare benchHash {manifestBenchHash}, le banc relu porte " +
                $"{bench.Manifest.BenchHash}. Reprendre une séance sur un autre banc est une " +
                "erreur de protocole.");
    }

    // ── Plomberie ───────────────────────────────────────────────────────────

    private static List<string> ReadNonEmptyLines(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Corpus humain introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new HumanIntegrityException($"Corpus humain vide : {path}");

        return lines;
    }

    private static T ReadManifest<T>(
        string path, string raw, Func<T, string> kindOf, Func<T, int> versionOf)
    {
        T manifest;
        try
        {
            manifest = EvalJson.Deserialize<T>(raw);
        }
        catch (JsonException ex)
        {
            throw new HumanIntegrityException($"Manifeste illisible dans {path} : {ex.Message}");
        }

        if (kindOf(manifest) != "manifest")
            throw new HumanIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={kindOf(manifest)}).");

        if (versionOf(manifest) != HarnessVersion)
            throw new HumanIntegrityException(
                $"{path} : harnessVersion {versionOf(manifest)} incompatible avec {HarnessVersion}.");

        return manifest;
    }

    private static string PeekKind(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("kind", out var kind)
            ? kind.GetString() ?? string.Empty
            : string.Empty;
    }

    private static void WarnTruncated(string path) =>
        Console.Error.WriteLine(
            $"AVERTISSEMENT : dernière ligne tronquée dans {path}, ignorée (séance interrompue). " +
            "La reprise la regénérera.");
}
