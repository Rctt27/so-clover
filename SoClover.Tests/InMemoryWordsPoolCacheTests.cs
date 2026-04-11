using SoClover.Domain;
using SoClover.Infrastructure;
using Xunit;

namespace SoClover.Tests;

public class InMemoryWordsPoolCacheTests
{
    private sealed class StaticWordDictionary : IWordDictionary
    {
        private readonly IReadOnlyList<string> _words;
        public StaticWordDictionary(IReadOnlyList<string> words) => _words = words;
        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
            => Task.FromResult(_words);
        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(_words.Take(count).ToList());
    }

    private static async Task<WordsPool> CreatePoolAsync()
    {
        var dict = new StaticWordDictionary(new[] { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel" });
        return await WordsPool.CreateAsync(GameId.New(), "test", dict);
    }

    [Fact]
    public void Get_returns_null_for_unknown_key()
    {
        var cache = new InMemoryWordsPoolCache();
        Assert.Null(cache.Get(GameId.New()));
    }

    [Fact]
    public async Task Set_then_Get_returns_same_instance()
    {
        var cache = new InMemoryWordsPoolCache();
        var gameId = GameId.New();
        var pool = await CreatePoolAsync();

        cache.Set(gameId, pool);

        Assert.Same(pool, cache.Get(gameId));
    }

    [Fact]
    public async Task Set_overwrites_existing_entry()
    {
        var cache = new InMemoryWordsPoolCache();
        var gameId = GameId.New();
        var first = await CreatePoolAsync();
        var second = await CreatePoolAsync();

        cache.Set(gameId, first);
        cache.Set(gameId, second);

        Assert.Same(second, cache.Get(gameId));
        Assert.NotSame(first, cache.Get(gameId));
    }

    [Fact]
    public async Task Remove_deletes_entry()
    {
        var cache = new InMemoryWordsPoolCache();
        var gameId = GameId.New();
        var pool = await CreatePoolAsync();
        cache.Set(gameId, pool);

        cache.Remove(gameId);

        Assert.Null(cache.Get(gameId));
    }

    [Fact]
    public void Remove_on_unknown_key_does_not_throw()
    {
        var cache = new InMemoryWordsPoolCache();
        var exception = Record.Exception(() => cache.Remove(GameId.New()));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Distinct_games_have_independent_entries()
    {
        var cache = new InMemoryWordsPoolCache();
        var idA = GameId.New();
        var idB = GameId.New();
        var poolA = await CreatePoolAsync();
        var poolB = await CreatePoolAsync();

        cache.Set(idA, poolA);
        cache.Set(idB, poolB);

        Assert.Same(poolA, cache.Get(idA));
        Assert.Same(poolB, cache.Get(idB));
        Assert.NotSame(cache.Get(idA), cache.Get(idB));
    }

    [Fact]
    public async Task Concurrent_set_and_get_on_distinct_keys_is_thread_safe()
    {
        var cache = new InMemoryWordsPoolCache();
        const int parallelism = 64;

        var ids = Enumerable.Range(0, parallelism).Select(_ => GameId.New()).ToArray();
        var pools = new WordsPool[parallelism];
        for (int i = 0; i < parallelism; i++)
            pools[i] = await CreatePoolAsync();

        Parallel.ForEach(Enumerable.Range(0, parallelism), i =>
        {
            cache.Set(ids[i], pools[i]);
            var read = cache.Get(ids[i]);
            Assert.Same(pools[i], read);
        });

        for (int i = 0; i < parallelism; i++)
            Assert.Same(pools[i], cache.Get(ids[i]));
    }
}
