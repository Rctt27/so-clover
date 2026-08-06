using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Scoring;

public sealed record ConfusionEntry(string Word, int Count);

/// <summary>
/// Effectifs derrière chaque taux. Un taux seul se lit comme un fait ; sur un petit dénominateur
/// il n'est parfois qu'un plancher d'estimateur — <c>strict_2of2</c> sur 22 directions ne peut
/// valoir que 0 ; 0,045 ; 0,091… Ces compteurs existent pour l'affichage : ils ne créent aucune
/// colonne de registre, les 18 colonnes sont préservées.
/// </summary>
public sealed record MetricCounts(
    int ValidItems,
    int FirstAttemptItems,
    int ParseFailures,
    int Attempts,
    int DecodedItems,
    int StrictItems,
    int HalfItems,
    int ScoredBoards,
    int SolvedBoards,
    int DecodeFailures,
    int Decodes,
    // Ventilés par prompt décodeur : decode-clue (N2) porte les quatre portes de calibration,
    // decode-board (N3) porte board_positions et M6. Un seul taux agrégé faisait accuser le
    // premier quand c'est le second qui décrochait — voir DecodeFailureWarningTests.
    int ClueDecodes = 0,
    int ClueDecodeFailures = 0,
    int BoardDecodes = 0,
    int BoardDecodeFailures = 0)
{
    public static readonly MetricCounts Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Les neuf indicateurs N1–N3, plus les deux indicateurs de santé. Aucun score unique : un
/// chiffre agrégé cacherait les compromis.
/// </summary>
public sealed record MetricsReport(
    string RunId,
    string BenchFile,
    string BenchHash,
    int BoardCount,
    int DirectionCount,
    double ValidRate,
    double FirstAttemptRate,
    double ParseFailureRate,
    double Recovery,
    double Strict2Of2,
    double HalfRate,
    double BoardPositions,
    double BoardSolvedFirstTry,
    IReadOnlyList<ConfusionEntry> ConfusionTop,
    double DecodeFailureRate,
    int ItemsCompleted,
    int ItemsExpected,
    // Détail par item, consommé par la comparaison appariée. Exclu de la sérialisation :
    // System.Text.Json ne sait pas écrire un dictionnaire à clé tuple, et ce détail n'a
    // aucun sens dans le fichier .metrics.json (il se recalcule depuis les fichiers de run).
    [property: JsonIgnore]
    IReadOnlyDictionary<(string BoardId, string Direction), double> PerItemRBar,
    MetricCounts Counts,
    string? SubsetFile = null,
    string? SubsetOutcome = null);

public static class RunMetrics
{
    public const int ConfusionTopSize = 10;

    /// <summary>
    /// <paramref name="subset"/> restreint la liste d'items ; <b>tous</b> les dénominateurs
    /// suivent, <see cref="MetricsReport.DirectionCount"/> compris — la restriction devient donc
    /// visible dans le rapport au lieu d'être une note de bas de page. Utilisé par le pseudo-run
    /// humain, qui ne couvre qu'une fraction du banc (§6.2 du design P4-P5).
    /// </summary>
    public static MetricsReport Compute(
        BenchContents bench, RunContents run, DecodeContents? decoded, int maxAttempts,
        IReadOnlySet<(string BoardId, string Direction)>? subset = null,
        string? subsetFile = null, string? subsetOutcome = null)
    {
        var items = bench.Boards
            .SelectMany(b => BoardGeometry.AllDirections.Select(d => (BoardId: b.BoardId, Direction: d.ToString())))
            .Where(item => subset is null || subset.Contains(item))
            .ToList();
        var directionCount = items.Count;
        var boardCount = subset is null
            ? bench.Manifest.BoardCount
            : items.Select(i => i.BoardId).Distinct(StringComparer.Ordinal).Count();

        // Le sous-ensemble ne restreint pas que les items : les tentatives, décodages et boards
        // hors périmètre sortent aussi des dénominateurs de santé, sinon `parse_failure_rate` et
        // `decode_failure_rate` parleraient d'un banc que le rapport ne prétend plus couvrir.
        var scope = items.ToHashSet();
        var scopedBoards = items.Select(i => i.BoardId).ToHashSet(StringComparer.Ordinal);
        var scopedAttempts = run.Attempts.Where(a => scope.Contains((a.BoardId, a.Direction))).ToList();
        var scopedClueDecodes = (decoded?.ClueDecodes ?? [])
            .Where(d => scope.Contains((d.BoardId, d.Direction))).ToList();
        var scopedBoardDecodes = (decoded?.BoardDecodes ?? [])
            .Where(b => scopedBoards.Contains(b.BoardId)).ToList();

        var attemptsByItem = scopedAttempts
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Attempt).ToList());

        // ---- N1 -------------------------------------------------------------
        var validItems = new HashSet<(string, string)>();
        var firstAttemptItems = new HashSet<(string, string)>();
        var completedItems = 0;

        foreach (var item in items)
        {
            if (!attemptsByItem.TryGetValue(item, out var attempts))
                continue;

            var winner = attempts.FirstOrDefault(a => a.Valid);
            if (winner is not null)
            {
                validItems.Add(item);
                if (winner.Attempt == 0)
                    firstAttemptItems.Add(item);
            }

            if (winner is not null || attempts.Count >= maxAttempts)
                completedItems++;
        }

        var parseFailures = scopedAttempts.Count(a => a.FailureKind is "empty" or "unparseable");

        // ---- N2 -------------------------------------------------------------
        var decodesByItem = scopedClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.DecodeIndex).ToList());

        var perItemRBar = new Dictionary<(string BoardId, string Direction), double>();
        var decodedItems = new List<(string BoardId, string Direction)>();
        var strictItems = 0;
        var halfItems = 0;

        foreach (var item in items)
        {
            // Une direction sans indice valide compte R̄ = 0 et RESTE au dénominateur :
            // un pipeline qui échoue à générer ne fait pas mieux qu'un pipeline qui génère
            // un indice indevinable (règle A-1 du PRD).
            if (!validItems.Contains(item) || !decodesByItem.TryGetValue(item, out var decodes))
            {
                perItemRBar[item] = 0.0;
                continue;
            }

            // Un décodage en échec de format est EXCLU du dénominateur de R̄, pas compté 0 :
            // un décodeur qui ne sait pas répondre au format n'est pas un décodeur qui se trompe.
            var scored = decodes.Where(d => d.DecodeFailureKind is null && d.R is not null).ToList();
            if (scored.Count == 0)
            {
                perItemRBar[item] = 0.0;
                continue;
            }

            perItemRBar[item] = scored.Average(d => d.R!.Value);
            decodedItems.Add(item);

            if (scored.All(d => d.R!.Value == 1.0))
                strictItems++;
            if (scored.Count(d => d.R!.Value == 0.5) >= 2)
                halfItems++;
        }

        var recovery = directionCount == 0 ? 0.0 : perItemRBar.Values.Sum() / directionCount;

        // ---- confusion_top : mots non-référence choisis, une fois par décodage ------
        var referenceByItem = bench.Boards.SelectMany(b =>
                BoardGeometry.AllDirections.Select(d => (
                    Key: (b.BoardId, d.ToString()),
                    Words: BenchBoardMapper.ReferenceWords(b, d))))
            .ToDictionary(x => x.Key, x => x.Words);

        var confusion = new Dictionary<string, int>();
        foreach (var decode in scopedClueDecodes)
        {
            if (decode.Picked is null) continue;
            if (!referenceByItem.TryGetValue((decode.BoardId, decode.Direction), out var reference)) continue;

            foreach (var word in decode.Picked.Where(w => !reference.Contains(w)))
                confusion[word] = confusion.GetValueOrDefault(word) + 1;
        }

        var confusionTop = confusion
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(ConfusionTopSize)
            .Select(kv => new ConfusionEntry(kv.Key, kv.Value))
            .ToList()
            .AsReadOnly();

        // ---- N3 -------------------------------------------------------------
        var scoredBoards = scopedBoardDecodes
            .Where(b => b.DecodeFailureKind is null && b.BoardPositions is not null)
            .ToList();

        var boardPositions = scoredBoards.Count == 0 ? 0.0 : scoredBoards.Average(b => b.BoardPositions!.Value);
        var boardSolvedFirstTry = scoredBoards.Count == 0 ? 0.0
            : scoredBoards.Count(b => b.BoardSolved == true) / (double)scoredBoards.Count;

        // ---- Santé ----------------------------------------------------------
        var decodeAttempts = scopedClueDecodes.Count + scopedBoardDecodes.Count;
        var decodeFailures = scopedClueDecodes.Count(d => d.DecodeFailureKind is not null)
                           + scopedBoardDecodes.Count(b => b.DecodeFailureKind is not null);

        return new MetricsReport(
            RunId: run.Manifest.RunId,
            BenchFile: run.Manifest.BenchFile,
            BenchHash: run.Manifest.BenchHash,
            BoardCount: boardCount,
            DirectionCount: directionCount,
            ValidRate: Ratio(validItems.Count, directionCount),
            FirstAttemptRate: Ratio(firstAttemptItems.Count, directionCount),
            // scopedAttempts, pas run.Attempts : le numérateur est déjà restreint, un dénominateur
            // sur le run entier rendrait un taux divisé par la part du banc couverte.
            ParseFailureRate: Ratio(parseFailures, scopedAttempts.Count),
            Recovery: recovery,
            Strict2Of2: Ratio(strictItems, decodedItems.Count),
            HalfRate: Ratio(halfItems, decodedItems.Count),
            BoardPositions: boardPositions,
            BoardSolvedFirstTry: boardSolvedFirstTry,
            ConfusionTop: confusionTop,
            DecodeFailureRate: Ratio(decodeFailures, decodeAttempts),
            ItemsCompleted: completedItems,
            ItemsExpected: directionCount,
            PerItemRBar: perItemRBar,
            Counts: new MetricCounts(
                ValidItems: validItems.Count,
                FirstAttemptItems: firstAttemptItems.Count,
                ParseFailures: parseFailures,
                Attempts: scopedAttempts.Count,
                DecodedItems: decodedItems.Count,
                StrictItems: strictItems,
                HalfItems: halfItems,
                ScoredBoards: scoredBoards.Count,
                SolvedBoards: scoredBoards.Count(b => b.BoardSolved == true),
                DecodeFailures: decodeFailures,
                Decodes: decodeAttempts,
                ClueDecodes: scopedClueDecodes.Count,
                ClueDecodeFailures: scopedClueDecodes.Count(d => d.DecodeFailureKind is not null),
                BoardDecodes: scopedBoardDecodes.Count,
                BoardDecodeFailures: scopedBoardDecodes.Count(b => b.DecodeFailureKind is not null)),
            SubsetFile: subsetFile,
            SubsetOutcome: subsetOutcome);
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0.0 : numerator / (double)denominator;
}
