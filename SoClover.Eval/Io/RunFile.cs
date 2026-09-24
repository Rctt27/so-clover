using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Io;

/// <summary>Le run lu ne correspond pas à ce qu'il déclare être.</summary>
// Même arbitrage que BenchIntegrityException : System.IO.InvalidDataException est `sealed`
// en .NET 9 (CS0509), la dérivation annoncée par le plan est impossible. Exception est la base
// la plus proche sans changer le contrat observable (Assert.Throws<RunIntegrityException>).
public sealed class RunIntegrityException : Exception
{
    public RunIntegrityException(string message) : base(message) { }
}

/// <summary>
/// Fichier de run JSONL append-only reprenable : ligne 1 = manifeste observé, lignes suivantes =
/// une par tentative. Écrire au fil de l'eau est ce qui rend un run de ~680 appels locaux
/// séquentiels interruptible sans perte.
/// </summary>
public static partial class RunFile
{
    public const int HarnessVersion = 1;

    /// <summary>
    /// 8 premiers caractères hex du SHA-256 du manifeste canonicalisé privé de <c>runId</c>,
    /// <c>createdAtUtc</c> et <c>operatorNotes</c>. Deux runs de configuration identique portent
    /// donc le même hash8 et se distinguent par la date — c'est voulu : un hash8 répété signale
    /// un re-run de la même configuration, ce que le PRD demande à chaque jalon pour détecter
    /// la dérive du modèle.
    /// </summary>
    public static string ComputeHash8(RunManifest manifest)
    {
        var canonical = manifest with
        {
            RunId = string.Empty,
            CreatedAtUtc = default,
            OperatorNotes = null,
            Prompt = null,
            Tracing = null,
        };
        return EvalJson.Sha256Hex(EvalJson.Serialize(canonical))[..8];
    }

    public static string BuildRunId(DateTime createdAtUtc, int? promptVersion, string modelId, string hash8)
    {
        var date = createdAtUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var version = promptVersion is { } v
            ? v.ToString(CultureInfo.InvariantCulture)
            : "none";
        return $"{date}-v{version}-{Sanitize(modelId)}-{hash8}";
    }

    /// <summary>Assainit un identifiant de modèle pour qu'il tienne dans un nom de fichier.</summary>
    public static string Sanitize(string value)
    {
        var lowered = NonSlugCharacters().Replace(value.ToLowerInvariant(), "-");
        return lowered.Trim('-');
    }

    public static void WriteManifest(string path, RunManifest manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            path,
            EvalJson.Serialize(manifest) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void AppendAttempt(string path, RunAttempt attempt) =>
        File.AppendAllText(
            path,
            EvalJson.Serialize(attempt) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    public static RunContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Run introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new RunIntegrityException($"Run vide : {path}");

        RunManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<RunManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new RunIntegrityException($"Manifeste de run illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "manifest")
            throw new RunIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={manifest.Kind}).");

        if (manifest.HarnessVersion != HarnessVersion)
            throw new RunIntegrityException(
                $"{path} : harnessVersion {manifest.HarnessVersion} incompatible avec {HarnessVersion}.");

        var attempts = new List<RunAttempt>(lines.Count - 1);
        for (var i = 1; i < lines.Count; i++)
        {
            try
            {
                attempts.Add(EvalJson.Deserialize<RunAttempt>(lines[i]));
            }
            catch (JsonException) when (i == lines.Count - 1)
            {
                // Dernière ligne tronquée : le run a été interrompu en pleine écriture.
                // On l'ignore — la reprise regénérera cette tentative.
                Console.Error.WriteLine(
                    $"AVERTISSEMENT : dernière ligne tronquée dans {path}, ignorée (run interrompu).");
            }
        }

        return new RunContents(manifest, attempts.AsReadOnly());
    }

    /// <summary>
    /// Directions terminées : celles qui ont une tentative <c>valid:true</c>, ou
    /// <paramref name="maxAttempts"/> tentatives consignées.
    /// </summary>
    public static IReadOnlySet<(string BoardId, string Direction)> CompletedDirections(
        RunContents run, int maxAttempts)
    {
        var completed = new HashSet<(string, string)>();
        foreach (var group in run.Attempts.GroupBy(a => (a.BoardId, a.Direction)))
        {
            if (group.Any(a => a.Valid) || group.Count() >= maxAttempts)
                completed.Add(group.Key);
        }
        return completed;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
