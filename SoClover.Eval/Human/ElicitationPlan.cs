using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

/// <summary>Une direction à traiter dans la séance A.</summary>
public sealed record PlanItem(string BoardId, string Direction);

/// <summary>
/// Tirage seedé des directions de la séance A, <b>reconstruit à chaque démarrage</b> depuis
/// <c>(benchHash, seed, targetCount)</c> : jamais persisté, donc jamais désynchronisé de la
/// reprise.
/// <para>
/// Le tirage est <b>uniforme</b> parmi les <c>boardCount × 4</c> directions, conformément au PRD.
/// Il n'impose donc pas un board distinct par direction : sur 40 tirages parmi 160, on attend
/// ~27 boards distincts. Un tirage qui garantirait 40 boards distincts serait stratifié, pas
/// uniforme.
/// </para>
/// <para>
/// Mélanger la totalité puis tronquer — plutôt que tirer sans remise jusqu'à <c>targetCount</c> —
/// donne gratuitement la propriété de <b>préfixe stable</b> : un plan de taille <c>N</c> est le
/// préfixe d'un plan de taille <c>N + k</c>, ce qui permet d'étendre une séance sans invalider ce
/// qui est déjà saisi.
/// </para>
/// </summary>
public static class ElicitationPlan
{
    public static IReadOnlyList<PlanItem> Build(BenchContents bench, long seed, int targetCount)
    {
        if (targetCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, "Le nombre de directions doit être > 0.");

        var all = bench.Boards
            .SelectMany(b => BoardGeometry.AllDirections.Select(d => new PlanItem(b.BoardId, d.ToString())))
            .ToList();

        if (targetCount > all.Count)
            throw new ArgumentOutOfRangeException(
                nameof(targetCount), targetCount,
                $"Le banc ne contient que {all.Count} directions.");

        new Xoshiro256SS(MixSeed(seed, bench.Manifest.BenchHash)).Shuffle(all);
        return all.Take(targetCount).ToList().AsReadOnly();
    }

    /// <summary>
    /// Mélange le seed de séance avec le <c>benchHash</c> : deux bancs distincts ne peuvent pas
    /// produire le même plan à seed égal. Passe par SHA-256 et jamais par
    /// <c>string.GetHashCode()</c>, randomisé par processus — un plan qui changerait d'un
    /// démarrage à l'autre casserait la reprise sans rien signaler.
    /// </summary>
    internal static long MixSeed(long seed, string benchHash)
    {
        var digest = EvalJson.Sha256Hex(benchHash);
        var mixed = ulong.Parse(digest[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return unchecked((long)(mixed ^ (ulong)seed));
    }
}
