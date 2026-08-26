using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

/// <summary>
/// Sémantique du cache de la sonde de clé API. Le point non trivial : seul un verdict
/// conclusif (Valid/Invalid) est mis en cache. Un Indeterminate ne l'est jamais — sinon un
/// blip réseau grillerait la feature pour toute la durée du TTL.
/// </summary>
public class CachedLlmApiKeyProbeTests
{
    private static readonly DateTime T0 = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    private sealed class ScriptedChecker : ILlmCredentialChecker
    {
        private readonly Queue<LlmCredentialStatus> _script = new();
        private readonly LlmCredentialStatus _fallback;

        public ScriptedChecker(LlmCredentialStatus fallback, params LlmCredentialStatus[] script)
        {
            _fallback = fallback;
            foreach (var s in script) _script.Enqueue(s);
        }

        public int CallCount { get; private set; }
        public Exception? ThrowOnCall { get; set; }

        public Task<LlmCredentialStatus> CheckAsync(CancellationToken ct = default)
        {
            CallCount++;
            if (ThrowOnCall is not null) throw ThrowOnCall;
            return Task.FromResult(_script.Count > 0 ? _script.Dequeue() : _fallback);
        }
    }

    private static (CachedLlmApiKeyProbe probe, ScriptedChecker checker, TestClock clock) Build(
        LlmCredentialStatus fallback,
        params LlmCredentialStatus[] script)
    {
        var checker = new ScriptedChecker(fallback, script);
        var clock = new TestClock(T0);
        var probe = new CachedLlmApiKeyProbe(checker, clock, TimeSpan.FromMinutes(5));
        return (probe, checker, clock);
    }

    [Fact]
    public async Task Valid_credential_reports_available()
    {
        var (probe, _, _) = Build(LlmCredentialStatus.Valid);

        Assert.True(await probe.IsAvailableAsync());
    }

    [Fact]
    public async Task Invalid_credential_reports_unavailable()
    {
        var (probe, _, _) = Build(LlmCredentialStatus.Invalid);

        Assert.False(await probe.IsAvailableAsync());
    }

    [Fact]
    public async Task Conclusive_verdict_is_cached_for_the_ttl()
    {
        var (probe, checker, clock) = Build(LlmCredentialStatus.Valid);

        await probe.IsAvailableAsync();
        clock.Advance(TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)));
        await probe.IsAvailableAsync();

        Assert.Equal(1, checker.CallCount);
    }

    [Fact]
    public async Task Cache_expires_after_the_ttl()
    {
        // Clé valide, puis révoquée entre-temps : le second appel doit refléter la révocation.
        var (probe, checker, clock) = Build(
            LlmCredentialStatus.Invalid,
            LlmCredentialStatus.Valid);

        Assert.True(await probe.IsAvailableAsync());

        clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        Assert.False(await probe.IsAvailableAsync());
        Assert.Equal(2, checker.CallCount);
    }

    [Fact]
    public async Task Indeterminate_reports_available_and_is_not_cached()
    {
        // Fail-open : un incident réseau ne prouve rien, on ne grise pas le bouton
        // et surtout on ne fige pas ce non-verdict pendant 5 minutes.
        var (probe, checker, _) = Build(LlmCredentialStatus.Indeterminate);

        Assert.True(await probe.IsAvailableAsync());
        Assert.True(await probe.IsAvailableAsync());

        Assert.Equal(2, checker.CallCount);
    }

    [Fact]
    public async Task Indeterminate_does_not_evict_a_previous_conclusive_verdict()
    {
        var (probe, checker, clock) = Build(
            LlmCredentialStatus.Valid,
            LlmCredentialStatus.Invalid,
            LlmCredentialStatus.Indeterminate);

        Assert.False(await probe.IsAvailableAsync());

        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await probe.IsAvailableAsync()); // Indeterminate → fail-open

        Assert.Equal(2, checker.CallCount);
    }

    [Fact]
    public async Task Checker_throwing_is_treated_as_indeterminate()
    {
        var (probe, checker, _) = Build(LlmCredentialStatus.Valid);
        checker.ThrowOnCall = new HttpRequestException("boom");

        Assert.True(await probe.IsAvailableAsync());
    }

    [Fact]
    public async Task Concurrent_callers_share_a_single_probe()
    {
        var (probe, checker, _) = Build(LlmCredentialStatus.Valid);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => probe.IsAvailableAsync()));

        Assert.Equal(1, checker.CallCount);
    }
}
