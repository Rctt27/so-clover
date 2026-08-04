using System.Globalization;
using System.Text;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Analysis;

public sealed record SampleItem(
    string BoardId,
    string Direction,
    string Clue,
    IReadOnlyList<string> ReferenceWords,
    IReadOnlyList<string> BoardWords,
    IReadOnlyList<string> DecodeLines,
    double RBar,
    string AutoMode,
    string? HumanMode);

public sealed record ConfusionRow(string AutoMode, string HumanMode, int Count);

public sealed record ConfusionMatrix(int Total, double Agreement, IReadOnlyList<ConfusionRow> Rows);

/// <summary>
/// Échantillon seedé d'échecs, rendu <b>lisible par un humain</b>, et relecture de l'étiquetage.
/// <para>
/// C'est la matérialisation du « lire ~20 échecs, les ranger en modes d'échec, compter » de la
/// boucle du PRD. Sans elle, on publie une distribution d'étiquettes que <b>personne n'a jamais
/// vérifiée</b> — et une taxonomie fausse est plus coûteuse qu'aucune taxonomie, puisqu'elle
/// oriente les interventions.
/// </para>
/// </summary>
public static class AnalysisSample
{
    public const int DefaultSize = 20;

    /// <summary>Seuil indicatif ; en dessous, les seuils des signatures sont à revoir — de façon datée.</summary>
    public const double ReviewAgreementThreshold = 0.70;

    private const string ClueKey = "- indice";
    private const string AutoKey = "- étiquette auto";
    private const string HumanKey = "- étiquette humaine";

    public static string PathFor(string directory, string runId) =>
        Path.Combine(directory, $"{runId}.sample.md");

    public static string TaxonomyPathFor(string directory, string runId) =>
        Path.Combine(directory, $"{runId}.taxonomy.json");

    public static IReadOnlyList<SampleItem> Draw(
        BenchContents bench, RunContents run, DecodeContents decoded,
        TaxonomyReport taxonomy, int size, long seed)
    {
        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        var clues = run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.Last().Clue!);

        var decodesByItem = decoded.ClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.DecodeIndex).ToList());

        // On tire parmi les ÉCHECS : lire 20 réussites n'apprendrait rien sur les modes d'échec.
        var candidates = taxonomy.Labels
            .Where(l => l.Mode != FailureModes.M0)
            .OrderBy(l => l.BoardId, StringComparer.Ordinal)
            .ThenBy(l => l.Direction, StringComparer.Ordinal)
            .ToList();

        new Xoshiro256SS(seed).Shuffle(candidates);

        return candidates
            .Take(Math.Min(size, candidates.Count))
            .Select(l =>
            {
                var board = boards[l.BoardId];
                var direction = Enum.Parse<Direction>(l.Direction);
                var key = (l.BoardId, l.Direction);
                var lines = decodesByItem.TryGetValue(key, out var d) ? d : [];

                return new SampleItem(
                    BoardId: l.BoardId,
                    Direction: l.Direction,
                    Clue: clues.TryGetValue(key, out var clue) ? clue : "— (aucun indice valide)",
                    ReferenceWords: BenchBoardMapper.ReferenceWords(board, direction),
                    BoardWords: BenchBoardMapper.AllWords(board),
                    DecodeLines: lines.Select(Describe).ToList().AsReadOnly(),
                    RBar: l.RBar,
                    AutoMode: l.Mode,
                    HumanMode: null);
            })
            .ToList()
            .AsReadOnly();
    }

    private static string Describe(ClueDecodeLine line) =>
        line.DecodeFailureKind is { } failure || line.Picked is null
            ? $"— (échec de format : {line.DecodeFailureKind ?? "inconnu"})"
            : $"{string.Join(" + ", line.Picked)}   (R = {Fr(line.R ?? 0)})";

    public static string Render(string runId, long seed, IReadOnlyList<SampleItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Échantillon d'échecs — {runId}");
        sb.AppendLine();
        sb.AppendLine($"> seed **{seed}** — {items.Count} item(s) tirés parmi les directions dont");
        sb.AppendLine($"> R̄ < {Fr(FailureTaxonomy.SuccessThreshold)}. Renseigner `étiquette humaine :` pour");
        sb.AppendLine("> **chaque** item, puis relancer :");
        sb.AppendLine(">");
        sb.AppendLine($"> `analyze --review eval/analysis/{runId}.sample.md`");
        sb.AppendLine(">");
        sb.AppendLine("> Vocabulaire fermé — " + string.Join(" · ",
            FailureModes.All.Select(m => $"`{m}` {FailureModes.Label(m)}")));
        sb.AppendLine(">");
        sb.AppendLine("> `M5` ne sort **jamais** automatiquement : c'est ici, et seulement ici, qu'il se pose.");
        sb.AppendLine();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            sb.AppendLine($"## {i + 1} — {item.BoardId} / {item.Direction}");
            sb.AppendLine();
            sb.AppendLine($"{ClueKey} : {item.Clue}");
            sb.AppendLine($"- paire cible : {string.Join(" + ", item.ReferenceWords)}");
            sb.AppendLine($"- R̄ : {Fr(item.RBar)}");
            sb.AppendLine($"- 16 mots : {string.Join(", ", item.BoardWords)}");
            foreach (var (line, index) in item.DecodeLines.Select((l, n) => (l, n)))
                sb.AppendLine($"- décodage {index} : {line}");
            sb.AppendLine($"{AutoKey} : {item.AutoMode}  ({FailureModes.Label(item.AutoMode)})");
            sb.AppendLine($"{HumanKey} :{(item.HumanMode is null ? string.Empty : " " + item.HumanMode)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Relit le fichier rempli. Ne reconstruit que ce dont la matrice de confusion a besoin —
    /// identifiant de l'item, indice, étiquette automatique, étiquette humaine.
    /// </summary>
    public static IReadOnlyList<SampleItem> Parse(string markdown)
    {
        var items = new List<SampleItem>();
        string? boardId = null, direction = null, clue = null, autoMode = null, humanMode = null;
        var decodeLines = new List<string>();

        void Flush()
        {
            if (boardId is null) return;
            items.Add(new SampleItem(
                boardId, direction ?? string.Empty, clue ?? string.Empty,
                [], [], decodeLines.ToList().AsReadOnly(), 0.0,
                autoMode ?? FailureModes.Unclassified, humanMode));
        }

        foreach (var raw in markdown.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            var line = raw.Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                boardId = direction = clue = autoMode = humanMode = null;
                decodeLines.Clear();

                // « ## 3 — dev-007 / Top »
                var header = line[3..].Split('—', 2).Last().Trim();
                var parts = header.Split('/', 2, StringSplitOptions.TrimEntries);
                boardId = parts[0];
                direction = parts.Length > 1 ? parts[1] : string.Empty;
                continue;
            }

            if (line.StartsWith(ClueKey, StringComparison.Ordinal)) clue = ValueOf(line);
            else if (line.StartsWith(AutoKey, StringComparison.Ordinal)) autoMode = ModeOf(ValueOf(line));
            else if (line.StartsWith(HumanKey, StringComparison.Ordinal))
            {
                var value = ValueOf(line);
                humanMode = value.Length == 0 ? null : ModeOf(value);
            }
            else if (line.StartsWith("- décodage ", StringComparison.Ordinal)) decodeLines.Add(ValueOf(line));
        }

        Flush();
        return items.AsReadOnly();
    }

    private static string ValueOf(string line)
    {
        var separator = line.IndexOf(':');
        return separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
    }

    /// <summary>Le premier mot de la valeur : « M2  (n'attrape qu'une face) » → « M2 ».</summary>
    private static string ModeOf(string value)
    {
        var mode = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        if (!FailureModes.IsKnown(mode))
            throw new ArgumentException(
                $"Étiquette « {mode} » hors du vocabulaire fermé : {string.Join(", ", FailureModes.All)}.");

        return mode;
    }

    public static ConfusionMatrix Review(IReadOnlyList<SampleItem> items)
    {
        var missing = items.Count(i => i.HumanMode is null);
        if (missing > 0)
            throw new InvalidOperationException(
                $"{missing} item(s) sur {items.Count} n'ont pas d'étiquette humaine. Une relecture " +
                "partielle n'est pas une relecture : remplir toutes les lignes « étiquette humaine : ».");

        var rows = items
            .GroupBy(i => (i.AutoMode, i.HumanMode))
            .OrderBy(g => g.Key.AutoMode, StringComparer.Ordinal)
            .ThenBy(g => g.Key.HumanMode, StringComparer.Ordinal)
            .Select(g => new ConfusionRow(g.Key.AutoMode, g.Key.HumanMode!, g.Count()))
            .ToList()
            .AsReadOnly();

        var agreeing = items.Count(i => i.AutoMode == i.HumanMode);
        return new ConfusionMatrix(
            items.Count,
            items.Count == 0 ? 0.0 : agreeing / (double)items.Count,
            rows);
    }

    private static string Fr(double value) =>
        value.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));
}
