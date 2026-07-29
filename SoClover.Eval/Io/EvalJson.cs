using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        Converters =
        {
            new ReadOnlyListConverter(),
        },
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

// ── Convertisseur JSON personnalisé ─────────────────────────────────────────

/// <summary>
/// Convertisseur pour IReadOnlyList&lt;T&gt; : désérialise en List&lt;T&gt; qui implémente
/// IReadOnlyList&lt;T&gt;, pour préserver le contenu sans imposer le type exact.
/// </summary>
internal sealed class ReadOnlyListConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        var genericDef = typeToConvert.GetGenericTypeDefinition();
        return genericDef == typeof(IReadOnlyList<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ReadOnlyListConverterGeneric<>).MakeGenericType(elementType);
        return (JsonConverter?)Activator.CreateInstance(converterType) ??
            throw new InvalidOperationException($"Impossible de créer le convertisseur pour {typeToConvert}");
    }
}

internal sealed class ReadOnlyListConverterGeneric<T> : JsonConverter<IReadOnlyList<T>>
{
    public override IReadOnlyList<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Attendu un tableau JSON, reçu {reader.TokenType}");

        var items = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var item = JsonSerializer.Deserialize<T>(ref reader, options);
            items.Add(item!);
        }

        return (IReadOnlyList<T>)items;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<T>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
            JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}
