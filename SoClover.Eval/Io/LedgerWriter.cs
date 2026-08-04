using System.Globalization;
using System.Text;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Io;

public sealed record LedgerEntry(
    DateOnly Date,
    string RunId,
    string BenchFile,
    string? PromptFile,
    int? PromptVersion,
    string ModelId,
    string? ModelSnapshotDate,
    string Settings,
    MetricsReport Metrics,
    string Status,
    string? Hypothesis,
    string Decision,
    string? OperatorNotes);

/// <summary>
/// Registre d'expériences : une ligne par run, <b>jamais réécrite</b>.
/// <para>
/// C'est le livrable le plus durable du chantier — celui qui transforme « j'ai l'impression que
/// v5 est meilleure » en affirmation défendable, et qui expliquera dans six mois pourquoi le
/// prompt contient telle instruction devenue mystérieuse.
/// </para>
/// </summary>
public static class LedgerWriter
{
    /// <summary>
    /// Statut des lignes de ce cycle : le décodeur n'a franchi que la porte du plancher
    /// aléatoire. Aucune ligne n'est défendable avant la calibration P6.
    /// </summary>
    public const string PreCalibrationStatus = "pré-calibration";

    /// <summary>Nombre de colonnes du tableau — invariant vérifié par les tests.</summary>
    public const int ColumnCount = 18;

    /// <summary>
    /// Statut d'une ligne défendable : le décodeur a franchi les <b>quatre</b> portes du PRD.
    /// <para>
    /// La cellule porte aussi l'<b>empreinte du décodeur</b> — c'est ce qui rendra lisible, dans
    /// six mois, l'effet du passage de <c>decode-clue</c> v1 à v2 : les deux lignes coexistent,
    /// aucune n'est réécrite, et l'empreinte dit laquelle se compare à laquelle.
    /// </para>
    /// </summary>
    public const string CalibratedStatus = "calibré";

    public static string CalibratedStatusFor(string decoderFingerprint) =>
        $"{CalibratedStatus} ({decoderFingerprint})";

    /// <summary>
    /// Substitut du pipe dans le texte libre. On NEUTRALISE plutôt qu'on n'échappe : un
    /// <c>\|</c> reste un vrai <c>|</c> dans la ligne, et tout relecteur qui découpe naïvement
    /// sur <c>|</c> (le réflexe naturel devant un tableau markdown) décalerait ses colonnes.
    /// </summary>
    private const char PipeSubstitute = '¦';

    private const string Header = """
        # Registre d'expériences — harnais d'évaluation des indices IA

        > Une ligne par run, **jamais réécrite**. Une correction s'ajoute, elle ne remplace pas.
        >
        > Statut `pré-calibration` : le décodeur n'a franchi que la porte du plancher aléatoire
        > (`recovery ≤ 0,15`). Les portes d'accord ≥ 75 % et κ ≥ 0,40 relèvent de la phase P6 —
        > **aucune ligne pré-calibration n'est défendable** au sens du PRD.
        >
        > Statut `calibré (<empreinte>)` : le décodeur a franchi les **quatre** portes (accord
        > ≥ 0,75, κ ≥ 0,40, non-saturation ≤ 0,95, plancher ≤ 0,15), consignées dans un fichier
        > `eval/human/calibration.<date>-<empreinte>.json`. L'**empreinte** identifie la config du
        > décodeur (modèle, prompt et sa version, température, topP, maxOutputTokens) — jamais le
        > nombre de décodages, qui change la granularité de R̄ et non le décodeur. **Deux `recovery`
        > d'empreintes différentes ne se comparent pas.**

        | date | runId | banc | prompt | version | modèle | snapshot | réglages | valid_rate | first_attempt | recovery | strict_2of2 | half_rate | board_solved_first_try | statut | décision | hypothèse | notes |
        |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
        """;

    public static void Append(string path, LedgerEntry entry)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (!File.Exists(path))
            File.WriteAllText(path, Header + "\n", utf8);

        File.AppendAllText(path, BuildRow(entry) + "\n", utf8);
    }

    /// <summary>Lignes de données du tableau, en-tête et séparateur exclus.</summary>
    public static IReadOnlyList<string> ReadRows(string path)
    {
        if (!File.Exists(path)) return [];

        return File.ReadAllLines(path)
            .Where(l => l.StartsWith('|'))
            .Where(l => !l.Contains("| date |"))
            .Where(l => !l.TrimStart('|').TrimStart().StartsWith("---", StringComparison.Ordinal))
            .ToList()
            .AsReadOnly();
    }

    private static string BuildRow(LedgerEntry e)
    {
        var m = e.Metrics;
        var cells = new[]
        {
            e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            e.RunId,
            Path.GetFileName(e.BenchFile),
            e.PromptFile is null ? "—" : Path.GetFileName(e.PromptFile),
            e.PromptVersion is { } v ? $"v{v}" : "—",
            e.ModelId,
            e.ModelSnapshotDate ?? "—",
            e.Settings,
            Number(m.ValidRate),
            Number(m.FirstAttemptRate),
            Number(m.Recovery),
            Number(m.Strict2Of2),
            Number(m.HalfRate),
            Number(m.BoardSolvedFirstTry),
            e.Status,
            e.Decision,
            e.Hypothesis ?? "—",
            e.OperatorNotes ?? "—",
        };

        if (cells.Length != ColumnCount)
            throw new InvalidOperationException(
                $"Le registre déclare {ColumnCount} colonnes mais la ligne en porte {cells.Length}.");

        return "| " + string.Join(" | ", cells.Select(Escape)) + " |";
    }

    // Le pipe est le séparateur de colonnes : le neutraliser plutôt que casser le tableau.
    private static string Escape(string cell) =>
        cell.Replace('|', PipeSubstitute).Replace('\n', ' ').Trim();

    private static string Number(double value) =>
        value.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));
}
