using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

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
}
