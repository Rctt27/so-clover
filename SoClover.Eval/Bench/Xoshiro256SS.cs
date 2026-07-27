namespace SoClover.Eval.Bench;

/// <summary>
/// PRNG déterministe xoshiro256** (Blackman &amp; Vigna), amorcé par SplitMix64.
/// <para>
/// Écrit à la main plutôt qu'emprunté à <see cref="System.Random"/> : la séquence de
/// <c>System.Random</c> n'est pas garantie stable entre versions du runtime .NET, et un banc
/// d'évaluation doit rester régénérable à l'identique dans deux ans. Le nom de l'algorithme est
/// consigné dans le manifeste du banc (<c>prngAlgorithm</c>) pour qu'un changement futur soit
/// visible plutôt que silencieux.
/// </para>
/// </summary>
public sealed class Xoshiro256SS
{
    public const string AlgorithmName = "xoshiro256ss";

    private ulong _s0, _s1, _s2, _s3;

    public Xoshiro256SS(long seed)
    {
        var state = unchecked((ulong)seed);
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        _s2 = SplitMix64(ref state);
        _s3 = SplitMix64(ref state);
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            var result = Rotl(_s1 * 5, 7) * 9;

            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);

            return result;
        }
    }

    /// <summary>
    /// Entier uniforme dans <c>[0, exclusiveUpperBound)</c>, sans biais de modulo
    /// (rejet de la tranche haute incomplète).
    /// </summary>
    public int NextInt(int exclusiveUpperBound)
    {
        if (exclusiveUpperBound <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(exclusiveUpperBound), exclusiveUpperBound, "La borne doit être > 0.");

        var bound = (ulong)exclusiveUpperBound;
        var threshold = unchecked(ulong.MaxValue - bound + 1) % bound;

        ulong draw;
        do
        {
            draw = NextUInt64();
        }
        while (draw < threshold);

        return (int)(draw % bound);
    }

    /// <summary>Fisher-Yates en place, descendant.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));
}
