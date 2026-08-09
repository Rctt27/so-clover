using System.Globalization;

namespace SoClover.Eval.Cli;

/// <summary>
/// Parsing minimal de la ligne de commande : un verbe positionnel, puis des paires
/// <c>--nom valeur</c> ou des drapeaux <c>--nom</c> sans valeur. Volontairement sans
/// dépendance externe : le harnais n'a que six verbes et une dizaine d'options.
/// </summary>
public sealed class Args
{
    private readonly Dictionary<string, List<string?>> _values;

    private Args(string verb, Dictionary<string, List<string?>> values)
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

        var values = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
        for (var i = verb.Length > 0 ? 1 : 0; i < argv.Length; i++)
        {
            if (!argv[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            var name = argv[i][2..];
            var hasValue = i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal);

            // Les occurrences s'ACCUMULENT au lieu de s'écraser : `--guessing a --guessing b`
            // désigne deux corpus. `Get` continue de rendre la dernière, donc rien de ce qui
            // existait ne change de comportement.
            if (!values.TryGetValue(name, out var list))
                values[name] = list = [];

            list.Add(hasValue ? argv[++i] : null);
        }

        return new Args(verb, values);
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? Get(string name) =>
        _values.TryGetValue(name, out var v) && v.Count > 0 ? v[^1] : null;

    /// <summary>
    /// Toutes les valeurs d'un drapeau répété, dans l'ordre de la ligne de commande. Les
    /// occurrences sans valeur (drapeau nu) sont écartées.
    /// </summary>
    public IReadOnlyList<string> GetAll(string name) =>
        _values.TryGetValue(name, out var v)
            ? v.Where(x => x is not null).Select(x => x!).ToList().AsReadOnly()
            : [];

    public string Require(string name) =>
        Get(name) ?? throw new ArgumentException($"Argument requis manquant : --{name}");

    // Le fallback n'est légitime que quand l'argument est ABSENT. S'il est présent mais mal
    // formé (ex : --seed abc), retomber silencieusement dessus produirait un run faux mais
    // plausible (ex : --decodes-per-clue x prendrait silencieusement le défaut 3). On échoue
    // fermé, avec le nom du drapeau et la valeur reçue dans le message.
    public int GetInt(string name, int fallback) =>
        Get(name) is not { } raw
            ? fallback
            : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new ArgumentException($"--{name} attend un entier, valeur reçue : \"{raw}\"");

    public long GetLong(string name, long fallback) =>
        Get(name) is not { } raw
            ? fallback
            : long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new ArgumentException($"--{name} attend un entier, valeur reçue : \"{raw}\"");
}
