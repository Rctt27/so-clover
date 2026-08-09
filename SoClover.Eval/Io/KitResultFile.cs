using System.Text.Json;
using SoClover.Eval.Human;

namespace SoClover.Eval.Io;

/// <summary>En-tête du fichier rapporté par le devineur hors ligne.</summary>
public sealed record KitResultManifest(
    string Kind,
    string BenchHash,
    long Seed,
    string RunId,
    string KitHash,
    int HarnessVersion,
    int ItemCount,
    string SessionId,
    DateTime StartedAtUtc,
    DateTime DownloadedAtUtc,
    string? UserAgent);

/// <summary>
/// Une réponse rapportée. <c>itemIndex</c> est la position dans le plan — la provenance
/// (<c>boardId</c>, direction) est réattachée à l'import, jamais transmise au devineur.
/// </summary>
public sealed record KitResultGuess(
    string Kind,
    int ItemIndex,
    IReadOnlyList<string> Picked,
    long ElapsedMs,
    DateTime GuessedAtUtc);

public sealed record KitResultContents(
    KitResultManifest Manifest,
    IReadOnlyList<KitResultGuess> Guesses);

/// <summary>
/// Lecture du JSONL produit par le kit hors ligne. C'est le <b>seul</b> fichier du harnais écrit
/// par une machine qu'on ne contrôle pas : il est relu avec méfiance et n'est jamais consommé tel
/// quel — <see cref="GuessKitImport"/> le confronte au plan reconstruit avant d'en dériver quoi
/// que ce soit.
/// </summary>
public static class KitResultFile
{
    public static KitResultContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Rapport de kit introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new HumanIntegrityException($"Rapport de kit vide : {path}");

        KitResultManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<KitResultManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new HumanIntegrityException($"En-tête illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "kit-manifest")
            throw new HumanIntegrityException(
                $"La première ligne de {path} n'est pas un en-tête de kit (kind={manifest.Kind}). " +
                "Le fichier attendu est celui produit par le bouton « Enregistrer mes réponses ».");

        var guesses = new List<KitResultGuess>();
        for (var i = 1; i < lines.Count; i++)
        {
            KitResultGuess guess;
            try
            {
                guess = EvalJson.Deserialize<KitResultGuess>(lines[i]);
            }
            catch (JsonException ex)
            {
                throw new HumanIntegrityException($"{path} ligne {i + 1} illisible : {ex.Message}");
            }

            if (guess.Kind != "kit-guess")
                throw new HumanIntegrityException(
                    $"{path} ligne {i + 1} : kind inconnu « {guess.Kind} ».");

            guesses.Add(guess);
        }

        return new KitResultContents(manifest, guesses.AsReadOnly());
    }
}
