using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Io;

/// <summary>
/// Sérialisation canonique du harnais. Les options sont figées : elles définissent la forme
/// exacte des lignes JSONL, donc les hachages de banc et de run. Toute modification invalide
/// l'historique du registre.
/// </summary>
public static class EvalJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Les bancs sont committés et relus par un humain : on ne veut pas de é partout.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidDataException($"Ligne JSON illisible pour le type {typeof(T).Name}.");

    public static string Sha256Hex(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Hash des items canonicalisés : chaque item sérialisé avec <see cref="Options"/>, joints
    /// par <c>\n</c>, puis SHA-256 tronqué à <paramref name="hexLength"/> caractères.
    /// </summary>
    public static string ComputeItemsHash<T>(IEnumerable<T> items, int hexLength)
    {
        var canonical = string.Join("\n", items.Select(Serialize));
        return Sha256Hex(canonical)[..hexLength];
    }

    /// <summary>
    /// Hash de gel du dictionnaire : le contenu normalisé (les mots effectivement chargés par
    /// <c>FileWordDictionary</c>, joints par <c>\n</c>) plutôt que les octets bruts du fichier.
    /// Immunise contre CRLF/LF, BOM et encodage — donc contre le système/la config Git de qui
    /// régénère un banc — sans toucher à la configuration Git du dépôt (arbitrage humain, Task 9).
    /// C'est une politique de gel arbitrée par un humain : elle vit ici, pas éparpillée dans un
    /// verbe CLI, pour rester appelable identiquement par le générateur et par toute vérification
    /// indépendante (tests d'intégrité des bancs committés).
    /// </summary>
    public static string DictionaryHash(IReadOnlyList<string> words) =>
        Sha256Hex(string.Join("\n", words))[..BenchGenerator.BenchHashHexLength];
}
