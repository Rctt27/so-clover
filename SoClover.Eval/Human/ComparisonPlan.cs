using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Human;

/// <summary>
/// Un couple à juger. <see cref="OptionA"/>/<see cref="OptionB"/> sont <b>canoniques</b> ;
/// <see cref="PresentedOrder"/> dit lequel s'affiche en position 1.
/// </summary>
public sealed record ComparisonPlanItem(
    string ComparisonId,
    string Family,
    string BoardId,
    string Direction,
    IReadOnlyList<string> ReferenceWords,
    ComparisonOption OptionA,
    ComparisonOption OptionB,
    string PresentedOrder,
    string? DuplicateOf);

/// <summary>
/// Composition déterministe du lot de la séance B depuis <c>(elicitation, runs, seed, target)</c>.
/// Comme le plan de la séance A, il n'est jamais persisté : il se reconstruit à l'identique à
/// chaque démarrage, ce qui rend la reprise sûre.
/// </summary>
public static class ComparisonPlan
{
    /// <summary>Détectent un juge distrait, pas la qualité du modèle.</summary>
    public const int AnchorCount = 5;

    /// <summary>Couples re-présentés plus tard dans l'ordre inverse → cohérence intra-juge.</summary>
    public const int DuplicateCount = 10;

    /// <summary>Écart minimal entre deux comparaisons partageant la même paire cible.</summary>
    public const int MinimumSeparation = 10;

    /// <summary>Suffixe de l'identifiant d'un doublon inversé.</summary>
    public const string DuplicateSuffix = "-r";

    /// <summary>Séparateur de champs du texte haché : U+001F, impossible dans un mot du jeu.</summary>
    private const string FieldSeparator = "\u001F";

    public static IReadOnlyList<ComparisonPlanItem> Build(
        BenchContents bench,
        ElicitationContents elicitation,
        RunContents modelA, string modelARunId,
        RunContents modelB, string modelBRunId,
        RunContents? anchorRun, string? anchorRunId,
        long seed,
        int targetCount)
    {
        var rng = new Xoshiro256SS(ElicitationPlan.MixSeed(seed, bench.Manifest.BenchHash));

        var cluesA = Index(modelA);
        var cluesB = Index(modelB);
        var cluesAnchor = anchorRun is null ? new Dictionary<(string, string), string>() : Index(anchorRun);

        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);
        IReadOnlyList<string> Reference(string boardId, string direction) =>
            BenchBoardMapper.ReferenceWords(boards[boardId], Enum.Parse<Direction>(direction));

        var annotated = elicitation.Elicitations
            .Select(e => (e.BoardId, e.Direction))
            .ToHashSet();

        var raw = new List<ComparisonPlanItem>();

        // ── humainVsModèle : les directions annotées effectivement résolues ──────
        foreach (var line in elicitation.Elicitations
                     .Where(e => e.Clue is not null && e.Outcome != Outcomes.Pass)
                     .OrderBy(e => e.BoardId, StringComparer.Ordinal)
                     .ThenBy(e => e.Direction, StringComparer.Ordinal))
        {
            if (!cluesA.TryGetValue((line.BoardId, line.Direction), out var modelClue))
                continue;

            Add(raw, ComparisonFamilies.HumanVsModel, line.BoardId, line.Direction, Reference,
                new ComparisonOption(ComparisonSources.Human, null, line.Clue!),
                new ComparisonOption(ComparisonSources.Model, modelARunId, modelClue));
        }

        // ── humainVsAssisté : la valeur ajoutée réelle de l'assistance ───────────
        foreach (var assist in elicitation.Assists
                     .Where(a => a.AssistedClue is not null)
                     .OrderBy(a => a.BoardId, StringComparer.Ordinal)
                     .ThenBy(a => a.Direction, StringComparer.Ordinal))
        {
            var human = elicitation.Elicitations.LastOrDefault(e =>
                e.BoardId == assist.BoardId && e.Direction == assist.Direction && e.Clue is not null);
            if (human is null) continue;

            Add(raw, ComparisonFamilies.HumanVsAssisted, assist.BoardId, assist.Direction, Reference,
                new ComparisonOption(ComparisonSources.Human, null, human.Clue!),
                new ComparisonOption(ComparisonSources.Assisted, null, assist.AssistedClue!));
        }

        // ── ancres : modèle × plancher aléatoire, hors directions annotées ───────
        var anchorPool = Pool(bench, annotated, cluesA, cluesAnchor);
        rng.Shuffle(anchorPool);
        var anchors = anchorPool.Take(AnchorCount).ToList();
        foreach (var key in anchors)
        {
            Add(raw, ComparisonFamilies.Anchor, key.BoardId, key.Direction, Reference,
                new ComparisonOption(ComparisonSources.Model, modelARunId, cluesA[key]),
                new ComparisonOption(ComparisonSources.Random, anchorRunId, cluesAnchor[key]));
        }

        // ── modèleVsModèle : remplit le lot, HORS directions annotées ────────────
        // Tirer hors des annotées évite qu'une même paire cible revienne trois fois dans la
        // séance — l'effet d'ancrage rendrait les jugements successifs dépendants — et élargit
        // la base d'items disponible pour la calibration P6.
        var used = anchors.ToHashSet();
        var modelPool = Pool(bench, annotated, cluesA, cluesB).Where(k => !used.Contains(k)).ToList();
        rng.Shuffle(modelPool);

        var baseTarget = Math.Max(0, targetCount - DuplicateCount);
        foreach (var key in modelPool)
        {
            if (raw.Count >= baseTarget) break;
            Add(raw, ComparisonFamilies.ModelVsModel, key.BoardId, key.Direction, Reference,
                new ComparisonOption(ComparisonSources.Model, modelARunId, cluesA[key]),
                new ComparisonOption(ComparisonSources.Model, modelBRunId, cluesB[key]));
        }

        // ── déduplication, mélange, équilibrage exact des ordres ─────────────────
        var basePlan = raw
            .GroupBy(i => i.ComparisonId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        rng.Shuffle(basePlan);
        basePlan = AssignOrders(basePlan, rng);

        // ── doublons inversés, autant depuis AB que depuis BA ────────────────────
        var duplicates = BuildDuplicates(basePlan, rng);

        var full = basePlan.Concat(duplicates).ToList();
        return SpaceOut(full, MinimumSeparation, i => i.BoardId + "|" + i.Direction).AsReadOnly();
    }

    /// <summary>
    /// Identifiant stable d'un couple : hash de <c>(boardId, direction, sources triées, indices
    /// triés)</c>. Indépendant de l'ordre des slots, donc identique d'une reconstruction du plan
    /// à l'autre — c'est ce qui rend la reprise sûre.
    /// </summary>
    public static string ComputeComparisonId(
        string boardId, string direction, ComparisonOption a, ComparisonOption b)
    {
        var sources = new[] { a.Source, b.Source }.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var clues = new[] { a.Clue, b.Clue }.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        string[] fields = [boardId, direction, sources[0], sources[1], clues[0], clues[1]];
        return "c-" + EvalJson.Sha256Hex(string.Join(FieldSeparator, fields))[..12];
    }

    /// <summary>
    /// Réordonne pour qu'aucune clé ne réapparaisse avant <paramref name="minGap"/> positions.
    /// Glouton stable : à chaque pas, le premier item dont la clé est « libre ». Si aucun ne
    /// l'est (fin de lot saturée d'une même clé), on prend le premier — la séance ne se bloque
    /// jamais sur une contrainte d'agencement.
    /// </summary>
    internal static List<T> SpaceOut<T>(IReadOnlyList<T> items, int minGap, Func<T, string> keyOf)
    {
        var remaining = new List<T>(items);
        var result = new List<T>(items.Count);
        var lastSeen = new Dictionary<string, int>(StringComparer.Ordinal);

        while (remaining.Count > 0)
        {
            var pick = 0;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (!lastSeen.TryGetValue(keyOf(remaining[i]), out var last) || result.Count - last >= minGap)
                {
                    pick = i;
                    break;
                }
            }

            var chosen = remaining[pick];
            remaining.RemoveAt(pick);
            lastSeen[keyOf(chosen)] = result.Count;
            result.Add(chosen);
        }

        return result;
    }

    // ── Interne ─────────────────────────────────────────────────────────────

    private static void Add(
        List<ComparisonPlanItem> into, string family, string boardId, string direction,
        Func<string, string, IReadOnlyList<string>> reference,
        ComparisonOption first, ComparisonOption second)
    {
        // Rien à juger : deux indices identiques ne portent aucune préférence.
        if (string.Equals(first.Clue.Trim(), second.Clue.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        var (a, b) = Canonical(first, second);
        into.Add(new ComparisonPlanItem(
            ComparisonId: ComputeComparisonId(boardId, direction, a, b),
            Family: family,
            BoardId: boardId,
            Direction: direction,
            ReferenceWords: reference(boardId, direction),
            OptionA: a,
            OptionB: b,
            PresentedOrder: PresentedOrders.Ab,
            DuplicateOf: null));
    }

    /// <summary>
    /// Ordre canonique des slots : par provenance, puis <c>runId</c>, puis indice. C'est ce qui
    /// rend <c>optionA</c>/<c>optionB</c> indépendants de l'ordre de présentation — donc le
    /// verdict interprétable sans connaître l'affichage.
    /// </summary>
    private static (ComparisonOption A, ComparisonOption B) Canonical(ComparisonOption x, ComparisonOption y)
    {
        var ordered = new[] { x, y }
            .OrderBy(o => ComparisonSources.Rank(o.Source))
            .ThenBy(o => o.RunId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(o => o.Clue, StringComparer.Ordinal)
            .ToArray();
        return (ordered[0], ordered[1]);
    }

    private static IReadOnlyDictionary<(string BoardId, string Direction), string> Index(RunContents run) =>
        run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.Last().Clue!);

    private static List<(string BoardId, string Direction)> Pool(
        BenchContents bench,
        IReadOnlySet<(string, string)> annotated,
        IReadOnlyDictionary<(string BoardId, string Direction), string> left,
        IReadOnlyDictionary<(string BoardId, string Direction), string> right) =>
        bench.Boards
            .SelectMany(b => BoardGeometry.AllDirections.Select(d => (BoardId: b.BoardId, Direction: d.ToString())))
            .Where(k => !annotated.Contains(k) && left.ContainsKey(k) && right.ContainsKey(k))
            .ToList();

    /// <summary>Équilibrage global : autant d'<c>AB</c> que de <c>BA</c> (à un près si impair).</summary>
    private static List<ComparisonPlanItem> AssignOrders(List<ComparisonPlanItem> items, Xoshiro256SS rng)
    {
        var orders = new List<string>(items.Count);
        for (var i = 0; i < items.Count; i++)
            orders.Add(i < items.Count / 2 ? PresentedOrders.Ab : PresentedOrders.Ba);
        rng.Shuffle(orders);

        return items.Select((item, i) => item with { PresentedOrder = orders[i] }).ToList();
    }

    private static List<ComparisonPlanItem> BuildDuplicates(
        List<ComparisonPlanItem> basePlan, Xoshiro256SS rng)
    {
        // Autant de doublons issus d'AB que de BA : leur inversion préserve alors exactement
        // l'équilibrage global du lot complet.
        var half = DuplicateCount / 2;
        var duplicates = new List<ComparisonPlanItem>(DuplicateCount);

        foreach (var order in new[] { PresentedOrders.Ab, PresentedOrders.Ba })
        {
            var pool = basePlan.Where(i => i.PresentedOrder == order).ToList();
            rng.Shuffle(pool);

            foreach (var source in pool.Take(half))
            {
                duplicates.Add(source with
                {
                    ComparisonId = source.ComparisonId + DuplicateSuffix,
                    PresentedOrder = PresentedOrders.Invert(source.PresentedOrder),
                    DuplicateOf = source.ComparisonId,
                });
            }
        }

        return duplicates;
    }
}
