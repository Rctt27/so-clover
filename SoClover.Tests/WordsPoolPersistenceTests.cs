using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;
using Xunit.Abstractions;

namespace SoClover.Tests;

/// <summary>
/// Regression suite for the WordsPool cache behavior.
/// The root bug: <c>Game._wordsPool</c> is a transient field lost on every JSON round-trip
/// through <c>EfGameRepository</c>. Without a cache, each repo.Get rehydrates a fresh pool,
/// so consumption history is lost and duplicate words can appear across cards.
///
/// These tests pin the fix in two complementary ways:
///   1. Instance identity — the cached WordsPool reference survives across use case calls.
///   2. Deterministic consumption — <c>RemainingWordsCount</c> decrements by exactly the
///      number of words drawn at each step. This is the load-bearing assertion; word-uniqueness
///      alone would be a probabilistic lottery (the dictionary Random is unseeded).
/// </summary>
public class WordsPoolPersistenceTests
{
    // Constants grounding the deterministic counts for a 4-player full flow.
    private const int PlayersCount = 4;
    private const int WordsPerCard = 4;
    private const int BoardCardsPerPlayer = 4;
    private const int WritingPhaseWordsConsumed = PlayersCount * BoardCardsPerPlayer * WordsPerCard; // 64
    private const int FifthCardWords = WordsPerCard; // 4

    private readonly ITestOutputHelper _output;

    public WordsPoolPersistenceTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Resolves the absolute path to SoClover/Infrastructure/Dictionaries from the test bin folder.
    /// Fails loud if the directory is missing so no test silently falls back to an unexpected source.
    /// </summary>
    private static string ResolveDictionaryPath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(
                $"Test setup error: word dictionaries directory not found at '{path}'. " +
                $"Expected relative layout: SoClover.Tests/bin/<cfg>/<tfm>/ → ../../../../SoClover/Infrastructure/Dictionaries.");

        return path;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        // Use the JSON-serializing repo to faithfully reproduce EfGameRepository's behavior:
        // every Save/Get cycles through JSON, so _wordsPool is lost on each load.
        services.AddSingleton<IGameRepository, JsonSerializingGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(ResolveDictionaryPath()));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IUpdateGameSettingsUseCase, UpdateGameSettings.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
        return services.BuildServiceProvider();
    }

    private static async Task<(GameId GameId, PlayerId AdminId)> CreateFourPlayerGameAsync(ServiceProvider sp)
    {
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));
        await join.Handle(new JoinGame.Request(createResp.GameId, "P2"));
        await join.Handle(new JoinGame.Request(createResp.GameId, "P3"));
        await join.Handle(new JoinGame.Request(createResp.GameId, "P4"));
        return (createResp.GameId, createResp.CreatorPlayerId);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Focused per-use-case tests
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CreateGame must seed the cache with a pool holding the full dictionary word count.
    /// </summary>
    [Fact]
    public async Task CreateGame_stores_full_pool_in_cache()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var dict = sp.GetRequiredService<IWordDictionary>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));

        var pool = cache.Get(createResp.GameId);
        Assert.NotNull(pool);

        var defaultLanguage = pool!.Language;
        var allWords = await dict.GetAllWordsAsync(defaultLanguage);
        Assert.Equal(allWords.Count, pool.RemainingWordsCount);
    }

    /// <summary>
    /// StartWritingPhase must consume exactly <see cref="WritingPhaseWordsConsumed"/> words
    /// from the *same* cached pool instance — proving the cache fix bridges the JSON round-trip.
    /// </summary>
    [Fact]
    public async Task StartWritingPhase_uses_cached_pool_and_decrements_count_deterministically()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var (gameId, _) = await CreateFourPlayerGameAsync(sp);
        var poolBefore = cache.Get(gameId);
        Assert.NotNull(poolBefore);
        var countBefore = poolBefore!.RemainingWordsCount;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        var poolAfter = cache.Get(gameId);
        Assert.NotNull(poolAfter);
        Assert.Same(poolBefore, poolAfter); // cached instance survived the JSON round-trip
        Assert.Equal(countBefore - WritingPhaseWordsConsumed, poolAfter!.RemainingWordsCount);
    }

    /// <summary>
    /// StartGuessingPhase generates the first 5th card and must draw exactly 4 words
    /// from the same cached pool.
    /// </summary>
    [Fact]
    public async Task StartGuessingPhase_uses_cached_pool_and_decrements_count_deterministically()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();

        var (gameId, _) = await CreateFourPlayerGameAsync(sp);
        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        var poolBefore = cache.Get(gameId);
        Assert.NotNull(poolBefore);
        var countBefore = poolBefore!.RemainingWordsCount;

        // Submit all boards so BoardsToGuess.Count > 0 (Epic 03 guard).
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await repo.Save(preGuessing);

        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, Force: true));

        var poolAfter = cache.Get(gameId);
        Assert.NotNull(poolAfter);
        Assert.Same(poolBefore, poolAfter);
        Assert.Equal(countBefore - FifthCardWords, poolAfter!.RemainingWordsCount);
    }

    /// <summary>
    /// MoveToNextBoard generates a new 5th card on non-final boards and must draw exactly 4 words
    /// from the cached pool. The final call uses a dummy "x,x,x,x" card and consumes nothing.
    /// </summary>
    [Fact]
    public async Task MoveToNextBoard_uses_cached_pool_and_decrements_count_deterministically()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var (gameId, adminId) = await CreateFourPlayerGameAsync(sp);
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        // Submit all boards so BoardsToGuess.Count > 0 (Epic 03 guard).
        var preGuessing = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await repo.Save(preGuessing);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, Force: true));

        var pool = cache.Get(gameId);
        Assert.NotNull(pool);
        var countBefore = pool!.RemainingWordsCount;

        await moveNext.Handle(new MoveToNextBoard.Request(gameId, adminId, InvocationOrigin.System));

        var poolAfter = cache.Get(gameId);
        Assert.Same(pool, poolAfter);
        Assert.Equal(countBefore - FifthCardWords, poolAfter!.RemainingWordsCount);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Full flow — the two flavors of Test A (with/without language change)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full flow without language change. Asserts deterministic pool decrement at every step.
    /// Without the cache fix, each <c>repo.Get</c> would rehydrate a fresh full pool and
    /// <c>RemainingWordsCount</c> would never drop below the starting count.
    /// </summary>
    [Fact]
    public async Task Full_flow_pool_consumption_deterministic()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var (gameId, adminId) = await CreateFourPlayerGameAsync(sp);

        var poolAfterCreate = cache.Get(gameId);
        Assert.NotNull(poolAfterCreate);
        var initialCount = poolAfterCreate!.RemainingWordsCount;
        _output.WriteLine($"After CreateGame: pool has {initialCount} words (language={poolAfterCreate.Language})");

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var expectedAfterWriting = initialCount - WritingPhaseWordsConsumed;
        Assert.Equal(expectedAfterWriting, cache.Get(gameId)!.RemainingWordsCount);
        _output.WriteLine($"After StartWritingPhase: {expectedAfterWriting} remaining (−{WritingPhaseWordsConsumed})");

        // Submit all boards so BoardsToGuess.Count > 0 (Epic 03 guard).
        var preGuessing1 = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing1.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await repo.Save(preGuessing1);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, Force: true));
        var expectedAfterGuessingStart = expectedAfterWriting - FifthCardWords;
        Assert.Equal(expectedAfterGuessingStart, cache.Get(gameId)!.RemainingWordsCount);
        _output.WriteLine($"After StartGuessingPhase: {expectedAfterGuessingStart} remaining (−{FifthCardWords})");

        // 3 MoveToNextBoard calls consume from the pool (−4 each). The 4th transitions to
        // Scoring with a dummy card and consumes nothing — we stop at 3 to keep assertions crisp.
        var runningCount = expectedAfterGuessingStart;
        for (int i = 0; i < PlayersCount - 1; i++)
        {
            await moveNext.Handle(new MoveToNextBoard.Request(gameId, adminId, InvocationOrigin.System));
            runningCount -= FifthCardWords;
            Assert.Equal(runningCount, cache.Get(gameId)!.RemainingWordsCount);
            _output.WriteLine($"After MoveToNextBoard #{i + 1}: {runningCount} remaining (−{FifthCardWords})");
        }

        // Full-flow total: 64 + 4 + 3×4 = 80 words consumed.
        Assert.Equal(initialCount - 80, cache.Get(gameId)!.RemainingWordsCount);

        // Cache identity must hold end-to-end — same instance since creation.
        Assert.Same(poolAfterCreate, cache.Get(gameId));
    }

    /// <summary>
    /// Same full flow, but the admin switches language Français → English in the lobby.
    /// The cache is replaced by a fresh English pool at the language change, then follows
    /// the same deterministic consumption pattern for the rest of the game.
    /// This is the scenario that revealed the bug in production.
    /// </summary>
    [Fact]
    public async Task Full_flow_with_language_change_preserves_pool_consumption()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var dict = sp.GetRequiredService<IWordDictionary>();
        var repo = sp.GetRequiredService<IGameRepository>();
        var updateSettings = sp.GetRequiredService<IUpdateGameSettingsUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var startGuessing = sp.GetRequiredService<IStartGuessingPhaseUseCase>();
        var moveNext = sp.GetRequiredService<IMoveToNextBoardUseCase>();

        var (gameId, adminId) = await CreateFourPlayerGameAsync(sp);

        // Change language to English — previous French pool is evicted and replaced.
        await updateSettings.Handle(new UpdateGameSettings.Request(
            gameId, adminId, "English_(from_FR_OFF)", null, null, null, null));

        var englishPool = cache.Get(gameId);
        Assert.NotNull(englishPool);
        Assert.Equal("English_(from_FR_OFF)", englishPool!.Language);
        var englishAllWords = await dict.GetAllWordsAsync("English_(from_FR_OFF)");
        var initialCount = englishPool.RemainingWordsCount;
        Assert.Equal(englishAllWords.Count, initialCount);
        _output.WriteLine($"After language change: English pool has {initialCount} words");

        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        var expectedAfterWriting = initialCount - WritingPhaseWordsConsumed;
        Assert.Equal(expectedAfterWriting, cache.Get(gameId)!.RemainingWordsCount);
        _output.WriteLine($"After StartWritingPhase: {expectedAfterWriting} remaining");

        // Submit all boards so BoardsToGuess.Count > 0 (Epic 03 guard).
        var preGuessing2 = await repo.Get(gameId) ?? throw new Exception();
        foreach (var pl in preGuessing2.ActivePlayers) pl.Board.MarkSubmitted(DateTime.UtcNow);
        await repo.Save(preGuessing2);
        await startGuessing.Handle(new StartGuessingPhase.Request(gameId, Force: true));
        var expectedAfterGuessingStart = expectedAfterWriting - FifthCardWords;
        Assert.Equal(expectedAfterGuessingStart, cache.Get(gameId)!.RemainingWordsCount);
        _output.WriteLine($"After StartGuessingPhase: {expectedAfterGuessingStart} remaining");

        var runningCount = expectedAfterGuessingStart;
        for (int i = 0; i < PlayersCount - 1; i++)
        {
            await moveNext.Handle(new MoveToNextBoard.Request(gameId, adminId, InvocationOrigin.System));
            runningCount -= FifthCardWords;
            Assert.Equal(runningCount, cache.Get(gameId)!.RemainingWordsCount);
            _output.WriteLine($"After MoveToNextBoard #{i + 1}: {runningCount} remaining");
        }

        Assert.Equal(initialCount - 80, cache.Get(gameId)!.RemainingWordsCount);
        Assert.Same(englishPool, cache.Get(gameId));
    }

    // ───────────────────────────────────────────────────────────────────────
    // Language-change cache semantics
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Changing language replaces the cached pool with a *fresh full* pool of the new language.
    /// Asserts both instance replacement and word-count equal to the English dictionary size.
    /// </summary>
    [Fact]
    public async Task Changing_language_replaces_cached_pool_with_full_new_pool()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var dict = sp.GetRequiredService<IWordDictionary>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var updateSettings = sp.GetRequiredService<IUpdateGameSettingsUseCase>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResp.GameId;
        var adminId = createResp.CreatorPlayerId;

        var frenchPool = cache.Get(gameId);
        Assert.NotNull(frenchPool);
        Assert.Equal("Français_OFF", frenchPool!.Language);

        await updateSettings.Handle(new UpdateGameSettings.Request(
            gameId, adminId, "English_(from_FR_OFF)", null, null, null, null));

        var englishPool = cache.Get(gameId);
        Assert.NotNull(englishPool);
        Assert.Equal("English_(from_FR_OFF)", englishPool!.Language);
        Assert.NotSame(frenchPool, englishPool);

        // The new pool must be full — same size as the underlying dictionary.
        var englishAllWords = await dict.GetAllWordsAsync("English_(from_FR_OFF)");
        Assert.Equal(englishAllWords.Count, englishPool.RemainingWordsCount);
    }

    /// <summary>
    /// Setting the same language is a no-op: the cached pool instance (and its consumption
    /// history) is preserved. Guards against unnecessary recreation on idempotent updates.
    /// </summary>
    [Fact]
    public async Task Setting_same_language_keeps_cached_pool()
    {
        var sp = BuildProvider();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var updateSettings = sp.GetRequiredService<IUpdateGameSettingsUseCase>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResp.GameId;
        var adminId = createResp.CreatorPlayerId;

        var poolBefore = cache.Get(gameId);
        Assert.NotNull(poolBefore);

        await updateSettings.Handle(new UpdateGameSettings.Request(
            gameId, adminId, poolBefore!.Language, null, null, null, null));

        var poolAfter = cache.Get(gameId);
        Assert.Same(poolBefore, poolAfter);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Known limitations and supporting infrastructure tests
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Documents a known limitation: deleting a game from the repo does NOT evict its pool
    /// from the cache. This is a small memory leak over time. If/when a repo.Delete callback
    /// is wired to <see cref="IWordsPoolCache.Remove"/>, flip this test's expected behavior.
    /// </summary>
    [Fact]
    public async Task Deleting_game_does_not_evict_pool_from_cache_known_limitation()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var cache = sp.GetRequiredService<IWordsPoolCache>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();

        var createResp = await create.Handle(new CreateGame.Request("Admin"));
        Assert.NotNull(cache.Get(createResp.GameId));

        await repo.Delete(createResp.GameId);

        // Pool leak: still present in the cache despite the game being gone. Documenting
        // current behavior, not endorsing it — revisit if Delete hooks into the cache.
        Assert.NotNull(cache.Get(createResp.GameId));
    }

    /// <summary>
    /// Smoke test for the test helper itself: <see cref="JsonSerializingGameRepository.GetAll"/>
    /// must return every persisted game. Guards against regressions in the fake repo — if this
    /// breaks, all other tests in this suite become unreliable.
    /// </summary>
    [Fact]
    public async Task JsonSerializingGameRepository_GetAll_returns_persisted_games()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var create = sp.GetRequiredService<ICreateGameUseCase>();

        var a = await create.Handle(new CreateGame.Request("Admin1"));
        var b = await create.Handle(new CreateGame.Request("Admin2"));
        var c = await create.Handle(new CreateGame.Request("Admin3"));

        var all = await repo.GetAll();
        Assert.Equal(3, all.Count);
        var ids = all.Select(g => g.Id).ToHashSet();
        Assert.Contains(a.GameId, ids);
        Assert.Contains(b.GameId, ids);
        Assert.Contains(c.GameId, ids);
    }
}
