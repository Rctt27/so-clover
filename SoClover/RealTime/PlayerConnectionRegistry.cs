using System.Collections.Concurrent;

namespace SoClover.RealTime;

/// <summary>
/// État de connexion partagé entre les instances (transientes) de <see cref="GameHub"/>,
/// enregistré en singleton. Encapsule les trois dictionnaires autrefois statiques dans le hub
/// (connexions, mapping joueur→partie, timers de grâce) et, surtout, l'<b>arbitrage atomique</b>
/// entre le rejoin et l'expiration de la période de grâce.
///
/// <para>
/// Bug d'origine (lobby mobile) : à l'expiration, la continuation du timer retirait le timer du
/// dictionnaire mais poursuivait l'éviction <i>quoi qu'il arrive</i>, et le <c>Cancel()</c> du rejoin
/// était un no-op une fois le <c>Task.Delay</c> terminé. Un joueur revenant pile à l'expiration
/// pouvait alors être ré-enregistré comme connecté <i>et</i> retiré du domaine → écran mort.
/// </para>
///
/// <para>
/// Correctif : <see cref="TryClaimExpiry"/> ne réclame l'éviction que si le timer est toujours le
/// timer courant <b>et</b> que le joueur n'est pas reconnecté ; et <see cref="AcquirePlayerLockAsync"/>
/// sérialise par joueur les sections critiques « rejoin » et « expiration » dans le hub, de sorte que
/// la décision d'arbitrage et la mutation domaine ne s'entrelacent jamais.
/// </para>
/// </summary>
public sealed class PlayerConnectionRegistry
{
    // playerId -> connectionId
    private readonly ConcurrentDictionary<string, string> _connections = new();
    // playerId -> gameId (conservé pendant la grâce, après retrait de la connexion)
    private readonly ConcurrentDictionary<string, string> _games = new();
    // playerId -> CTS du timer de grâce courant
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();
    // playerId -> verrou de sérialisation rejoin/expiration
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    // Sérialise l'arbitrage (claim/cancel/register) entre threads pour garantir l'atomicité
    // de la décision face à TryClaimExpiry.
    private readonly object _arbitration = new();

    public bool IsConnected(string playerId) => _connections.ContainsKey(playerId);

    public bool TryGetGame(string playerId, out string gameId)
    {
        var found = _games.TryGetValue(playerId, out var g);
        gameId = g ?? string.Empty;
        return found;
    }

    /// <summary>Enregistre (ou rafraîchit) la connexion d'un joueur et son mapping partie.</summary>
    public void RegisterConnection(string playerId, string connectionId, string gameId)
    {
        lock (_arbitration)
        {
            _connections[playerId] = connectionId;
            _games[playerId] = gameId;
        }
    }

    /// <summary>
    /// Retire la connexion identifiée par <paramref name="connectionId"/>. Conserve le mapping
    /// joueur→partie (le timer de grâce en a besoin pour décider de l'éviction).
    /// </summary>
    public bool TryRemoveConnection(string connectionId, out string playerId, out string gameId)
    {
        lock (_arbitration)
        {
            var match = _connections.FirstOrDefault(kv => kv.Value == connectionId);
            if (match.Key == null)
            {
                playerId = string.Empty;
                gameId = string.Empty;
                return false;
            }

            playerId = match.Key;
            _connections.TryRemove(playerId, out _);
            gameId = _games.TryGetValue(playerId, out var g) ? g : string.Empty;
            return true;
        }
    }

    /// <summary>Enregistre le CTS du timer de grâce courant, remplaçant tout timer antérieur.</summary>
    public void RegisterGraceTimer(string playerId, CancellationTokenSource cts)
    {
        lock (_arbitration)
        {
            _timers[playerId] = cts;
        }
    }

    /// <summary>
    /// Annule et retire le timer de grâce du joueur (chemin rejoin). No-op s'il n'y en a pas.
    /// Ne dispose pas le CTS : la continuation du timer en est propriétaire (dispose dans son
    /// <c>finally</c>), pour éviter une <see cref="ObjectDisposedException"/> sur l'accès au token.
    /// </summary>
    public void CancelGraceTimer(string playerId)
    {
        lock (_arbitration)
        {
            if (_timers.TryRemove(playerId, out var cts))
            {
                cts.Cancel();
            }
        }
    }

    /// <summary>
    /// Réclame le droit d'évincer le joueur à l'expiration de la grâce. Renvoie <c>true</c>
    /// uniquement si <paramref name="expectedCts"/> est toujours le timer courant <b>et</b> que le
    /// joueur n'est pas reconnecté. En cas de succès, retire le timer et le mapping partie (l'appelant
    /// dispose le CTS). Sinon, ne touche à rien : un rejoin a gagné la course.
    /// </summary>
    public bool TryClaimExpiry(string playerId, CancellationTokenSource expectedCts)
    {
        lock (_arbitration)
        {
            if (!_timers.TryGetValue(playerId, out var current) || !ReferenceEquals(current, expectedCts))
                return false;
            if (_connections.ContainsKey(playerId))
                return false;

            _timers.TryRemove(playerId, out _);
            _games.TryRemove(playerId, out _);
            return true;
        }
    }

    /// <summary>
    /// Acquiert le verrou propre au joueur. Le hub l'utilise pour sérialiser le rejoin et la
    /// continuation d'expiration : ainsi, la vérification d'appartenance + ré-enregistrement du
    /// rejoin et la décision + éviction de l'expiration ne s'entrelacent jamais.
    /// </summary>
    public async Task<IDisposable> AcquirePlayerLockAsync(string playerId)
    {
        var gate = _locks.GetOrAdd(playerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        return new Releaser(gate);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _gate;
        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public void Dispose()
        {
            _gate?.Release();
            _gate = null;
        }
    }
}
