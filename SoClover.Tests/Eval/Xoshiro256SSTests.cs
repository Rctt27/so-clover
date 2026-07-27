using SoClover.Eval.Bench;
using Xunit;

namespace SoClover.Tests.Eval;

public class Xoshiro256SSTests
{
    [Fact]
    public void AlgorithmName_is_recorded_for_the_bench_manifest()
    {
        Assert.Equal("xoshiro256ss", Xoshiro256SS.AlgorithmName);
    }

    [Fact]
    public void Same_seed_yields_the_same_sequence()
    {
        var a = new Xoshiro256SS(20260726001);
        var b = new Xoshiro256SS(20260726001);

        for (var i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void Different_seeds_yield_different_sequences()
    {
        var a = new Xoshiro256SS(20260726001);
        var b = new Xoshiro256SS(20260726002);

        var differing = 0;
        for (var i = 0; i < 100; i++)
            if (a.NextUInt64() != b.NextUInt64()) differing++;

        Assert.True(differing > 95, $"séquences trop proches : {differing}/100 valeurs diffèrent");
    }

    [Fact]
    public void NextInt_stays_within_bounds()
    {
        var rng = new Xoshiro256SS(42);

        for (var i = 0; i < 10_000; i++)
        {
            var v = rng.NextInt(880);
            Assert.InRange(v, 0, 879);
        }
    }

    [Fact]
    public void NextInt_rejects_a_non_positive_bound()
    {
        var rng = new Xoshiro256SS(42);

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-3));
    }

    [Fact]
    public void NextInt_is_reasonably_uniform_over_a_small_range()
    {
        var rng = new Xoshiro256SS(7);
        var buckets = new int[10];

        for (var i = 0; i < 100_000; i++)
            buckets[rng.NextInt(10)]++;

        // Tolérance large : on détecte un biais grossier, pas une déviation statistique fine.
        Assert.All(buckets, count => Assert.InRange(count, 9_000, 11_000));
    }

    [Fact]
    public void Shuffle_is_a_permutation()
    {
        var source = Enumerable.Range(0, 880).ToList();
        var shuffled = source.ToList();

        new Xoshiro256SS(20260726001).Shuffle(shuffled);

        Assert.Equal(source.Count, shuffled.Count);
        Assert.Equal(source.OrderBy(x => x), shuffled.OrderBy(x => x));
        Assert.NotEqual(source, shuffled);
    }

    [Fact]
    public void Shuffle_is_reproducible_for_a_given_seed()
    {
        var a = Enumerable.Range(0, 100).ToList();
        var b = Enumerable.Range(0, 100).ToList();

        new Xoshiro256SS(123).Shuffle(a);
        new Xoshiro256SS(123).Shuffle(b);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Shuffle_handles_degenerate_sizes()
    {
        var empty = new List<int>();
        var single = new List<int> { 42 };

        new Xoshiro256SS(1).Shuffle(empty);
        new Xoshiro256SS(1).Shuffle(single);

        Assert.Empty(empty);
        Assert.Equal([42], single);
    }

    // Verrou de non-régression : si l'algorithme change, ce test casse et le changement
    // devient une décision explicite plutôt qu'une dérive silencieuse des bancs committés.
    [Fact]
    public void First_draws_for_the_dev_seed_are_frozen()
    {
        var rng = new Xoshiro256SS(20260726001);

        var drawn = new[] { rng.NextInt(880), rng.NextInt(880), rng.NextInt(880) };

        // Valeurs à renseigner à la première exécution verte (cf. Step 4).
        Assert.Equal(new[] { 709, 772, 664 }, drawn);
    }
}
