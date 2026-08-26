using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Mémoïse le verdict de <see cref="ILlmCredentialChecker"/> pendant un TTL court, pour que
/// <c>/api/config</c> puisse être sondé à chaque appel sans marteler le provider.
///
/// Deux règles portent toute la sémantique :
/// <list type="bullet">
/// <item>seul un verdict conclusif (Valid/Invalid) est mis en cache ; un
/// <see cref="LlmCredentialStatus.Indeterminate"/> est retenté au prochain appel ;</item>
/// <item>un Indeterminate est fail-open (« disponible ») : une panne réseau n'est pas une preuve
/// de révocation, et le comportement historique — feature pilotée par le seul flag — reste
/// le fallback.</item>
/// </list>
///
/// Enregistré en singleton : le cache et le sémaphore sont volontairement process-wide.
/// </summary>
public sealed class CachedLlmApiKeyProbe : ILlmApiKeyProbe
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly ILlmCredentialChecker _checker;
    private readonly IClock _clock;
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _cachedVerdict;
    private DateTime? _cachedUntil;

    public CachedLlmApiKeyProbe(ILlmCredentialChecker checker, IClock clock, TimeSpan? ttl = null)
    {
        _checker = checker;
        _clock = clock;
        _ttl = ttl ?? DefaultTtl;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (TryReadCache(out var cached))
            return cached;

        // Le sémaphore évite qu'une rafale de clients au boot ne déclenche N sondes simultanées.
        await _gate.WaitAsync(ct);
        try
        {
            // Re-lecture : un appelant concurrent a pu remplir le cache pendant l'attente.
            if (TryReadCache(out cached))
                return cached;

            var status = await SafeCheckAsync(ct);

            if (status == LlmCredentialStatus.Indeterminate)
                return true;

            _cachedVerdict = status == LlmCredentialStatus.Valid;
            _cachedUntil = _clock.UtcNow.Add(_ttl);
            return _cachedVerdict;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryReadCache(out bool verdict)
    {
        var until = _cachedUntil;
        if (until.HasValue && _clock.UtcNow < until.Value)
        {
            verdict = _cachedVerdict;
            return true;
        }

        verdict = false;
        return false;
    }

    private async Task<LlmCredentialStatus> SafeCheckAsync(CancellationToken ct)
    {
        try
        {
            return await _checker.CheckAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Un checker qui lève n'a rien prouvé sur la clé — même traitement qu'un timeout.
            return LlmCredentialStatus.Indeterminate;
        }
    }
}
