using System.Globalization;

namespace SoClover.Eval.Cli;

/// <summary>
/// Parsing minimal de la ligne de commande : un verbe positionnel, puis des paires
/// <c>--nom valeur</c> ou des drapeaux <c>--nom</c> sans valeur. Volontairement sans
/// dépendance externe : le harnais n'a que six verbes et une dizaine d'options.
/// </summary>
public sealed class Args
{
    private readonly Dictionary<string, string?> _values;

    private Args(string verb, Dictionary<string, string?> values)
    {
        Verb = verb;
        _values = values;
    }

    public string Verb { get; }

    public static Args Parse(string[] argv)
    {
        var verb = argv.Length > 0 && !argv[0].StartsWith("--", StringComparison.Ordinal)
            ? argv[0]
            : string.Empty;

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = verb.Length > 0 ? 1 : 0; i < argv.Length; i++)
        {
            if (!argv[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            var name = argv[i][2..];
            var hasValue = i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal);
            values[name] = hasValue ? argv[++i] : null;
        }

        return new Args(verb, values);
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;

    public string Require(string name) =>
        Get(name) ?? throw new ArgumentException($"Argument requis manquant : --{name}");

    public int GetInt(string name, int fallback) =>
        Get(name) is { } raw && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;

    public long GetLong(string name, long fallback) =>
        Get(name) is { } raw && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
}
