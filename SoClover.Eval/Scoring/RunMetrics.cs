using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Scoring;

public sealed record ConfusionEntry(string Word, int Count);

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
    double BoardSolved,
    IReadOnlyList<ConfusionEntry> ConfusionTop,
    double DecodeFailureRate,
    int ItemsCompleted,
    int ItemsExpected,
    // Détail par item, consommé par la comparaison appariée. Exclu de la sérialisation :
    // System.Text.Json ne sait pas écrire un dictionnaire à clé tuple, et ce détail n'a
    // aucun sens dans le fichier .metrics.json (il se recalcule depuis les fichiers de run).
    [property: JsonIgnore]
    IReadOnlyDictionary<(string BoardId, string Direction), double> PerItemRBar);

public static class RunMetrics
{
    public const int ConfusionTopSize = 10;

    public static MetricsReport Compute(
        BenchContents bench, RunContents run, DecodeContents? decoded, int maxAttempts)
    {
        var items = bench.Boards
            .SelectMany(b => BoardGeometry.AllDirections.Select(d => (BoardId: b.BoardId, Direction: d.ToString())))
            .ToList();
        var directionCount = items.Count;

        var attemptsByItem = run.Attempts
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

        var parseFailures = run.Attempts.Count(a => a.FailureKind is "empty" or "unparseable");

        // ---- N2 -------------------------------------------------------------
        var decodesByItem = (decoded?.ClueDecodes ?? [])
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
        foreach (var decode in decoded?.ClueDecodes ?? [])
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
        var scoredBoards = (decoded?.BoardDecodes ?? [])
            .Where(b => b.DecodeFailureKind is null && b.BoardPositions is not null)
            .ToList();

        var boardPositions = scoredBoards.Count == 0 ? 0.0 : scoredBoards.Average(b => b.BoardPositions!.Value);
        var boardSolved = scoredBoards.Count == 0 ? 0.0
            : scoredBoards.Count(b => b.BoardSolved == true) / (double)scoredBoards.Count;

        // ---- Santé ----------------------------------------------------------
        var decodeAttempts = (decoded?.ClueDecodes.Count ?? 0) + (decoded?.BoardDecodes.Count ?? 0);
        var decodeFailures = (decoded?.ClueDecodes.Count(d => d.DecodeFailureKind is not null) ?? 0)
                           + (decoded?.BoardDecodes.Count(b => b.DecodeFailureKind is not null) ?? 0);

        return new MetricsReport(
            RunId: run.Manifest.RunId,
            BenchFile: run.Manifest.BenchFile,
            BenchHash: run.Manifest.BenchHash,
            BoardCount: bench.Manifest.BoardCount,
            DirectionCount: directionCount,
            ValidRate: Ratio(validItems.Count, directionCount),
            FirstAttemptRate: Ratio(firstAttemptItems.Count, directionCount),
            ParseFailureRate: Ratio(parseFailures, run.Attempts.Count),
            Recovery: recovery,
            Strict2Of2: Ratio(strictItems, decodedItems.Count),
            HalfRate: Ratio(halfItems, decodedItems.Count),
            BoardPositions: boardPositions,
            BoardSolved: boardSolved,
            ConfusionTop: confusionTop,
            DecodeFailureRate: Ratio(decodeFailures, decodeAttempts),
            ItemsCompleted: completedItems,
            ItemsExpected: directionCount,
            PerItemRBar: perItemRBar);
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0.0 : numerator / (double)denominator;
}
