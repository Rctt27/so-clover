using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Human;

/// <summary>Une direction à deviner dans la séance D, avec l'indice qui sera présenté.</summary>
public sealed record GuessPlanItem(string BoardId, string Direction, string Clue);

/// <summary>
/// Tirage seedé de la séance D « devineur », <b>reconstruit à chaque démarrage</b> depuis
/// <c>(benchHash, seed, boards éligibles, run source)</c> — jamais persisté, donc jamais
/// désynchronisé de la reprise. Même invariant que <see cref="ElicitationPlan"/>.
/// <para>
/// Contrairement aux séances A et B, le plan ne tire pas un sous-ensemble : il prend
/// <b>toutes</b> les directions des boards éligibles qui portent un indice valide. La sélection
/// vit dans <paramref name="excludedBoardIds"/>, calculé par l'appelant — le plan ne connaît ni la
/// séance A ni les boards exposés hors protocole, et n'a pas à les connaître.
/// </para>
/// </summary>
public static class GuessingPlan
{
    /// <summary>
    /// Séparation visée entre deux directions d'un même board. L'humain garde la mémoire d'un
    /// board d'une direction à l'autre là où chaque appel du décodeur est indépendant : c'est la
    /// cinquième limite du pré-enregistrement, et disperser les items est sa mitigation déclarée.
    /// <see cref="ComparisonPlan.SpaceOut{T}"/> est glouton — il ne garantit pas cet écart, il
    /// l'approche au mieux — mais il garantit qu'aucune répétition n'est <i>consécutive</i> tant
    /// qu'un autre board reste disponible.
    /// </summary>
    private const int MinimumBoardSeparation = 8;

    public static IReadOnlyList<GuessPlanItem> Build(
        BenchContents bench,
        RunContents run,
        IReadOnlySet<string> excludedBoardIds,
        long seed)
    {
        // Le premier essai valide de chaque direction — RunFile consigne une ligne par tentative,
        // jamais une par direction.
        var clues = run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Attempt).First().Clue!);

        var items = bench.Boards
            .Where(b => !excludedBoardIds.Contains(b.BoardId))
            .SelectMany(b => BoardGeometry.AllDirections.Select(d => (b.BoardId, Direction: d.ToString())))
            .Where(k => clues.ContainsKey(k))
            .Select(k => new GuessPlanItem(k.BoardId, k.Direction, clues[k]))
            .ToList();

        new Xoshiro256SS(ElicitationPlan.MixSeed(seed, bench.Manifest.BenchHash)).Shuffle(items);

        return ComparisonPlan
            .SpaceOut(items, MinimumBoardSeparation, i => i.BoardId)
            .AsReadOnly();
    }
}
