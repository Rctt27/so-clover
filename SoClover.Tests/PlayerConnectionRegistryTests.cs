using System.Threading;
using System.Threading.Tasks;
using SoClover.RealTime;
using Xunit;

namespace SoClover.Tests;

/// <summary>
/// Couvre l'arbitrage expiration↔reconnexion (race du bug lobby mobile) et le verrou
/// par joueur qui sérialise le rejoin et l'expiration de grâce dans GameHub.
/// </summary>
public class PlayerConnectionRegistryTests
{
    private const string Player = "11111111-1111-1111-1111-111111111111";
    private const string Conn = "conn-A";
    private const string Game = "GAME-1";

    [Fact]
    public void TryClaimExpiry_returns_false_when_player_reconnected()
    {
        var registry = new PlayerConnectionRegistry();
        var cts = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, cts);

        // Le joueur revient avant l'expiration : nouvelle connexion enregistrée.
        registry.RegisterConnection(Player, Conn, Game);

        // L'expiration ne doit PAS réclamer l'éviction d'un joueur reconnecté.
        Assert.False(registry.TryClaimExpiry(Player, cts));
    }

    [Fact]
    public void TryClaimExpiry_returns_false_when_timer_was_cancelled_by_join()
    {
        var registry = new PlayerConnectionRegistry();
        var cts = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, cts);

        // Le rejoin annule le timer de grâce.
        registry.CancelGraceTimer(Player);

        // Le timer périmé ne doit pas pouvoir évincer.
        Assert.False(registry.TryClaimExpiry(Player, cts));
    }

    [Fact]
    public void TryClaimExpiry_returns_true_on_genuine_expiry_then_false_when_already_claimed()
    {
        var registry = new PlayerConnectionRegistry();
        var cts = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, cts);

        // Pas de reconnexion : l'éviction est légitime.
        Assert.True(registry.TryClaimExpiry(Player, cts));
        // Idempotent : un second appel ne réclame pas deux fois.
        Assert.False(registry.TryClaimExpiry(Player, cts));
    }

    [Fact]
    public void TryClaimExpiry_returns_false_for_a_superseded_timer()
    {
        var registry = new PlayerConnectionRegistry();
        var ctsOld = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, ctsOld);

        // Nouvelle déconnexion → nouveau timer remplace l'ancien.
        var ctsNew = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, ctsNew);

        // L'ancien timer ne doit pas pouvoir évincer (il n'est plus le timer courant).
        Assert.False(registry.TryClaimExpiry(Player, ctsOld));
        // Le timer courant, lui, le peut.
        Assert.True(registry.TryClaimExpiry(Player, ctsNew));
    }

    [Fact]
    public void TryClaimExpiry_clears_game_mapping_on_claim()
    {
        var registry = new PlayerConnectionRegistry();
        registry.RegisterConnection(Player, Conn, Game);
        registry.TryRemoveConnection(Conn, out _, out _); // déconnexion : connexion retirée, mapping conservé
        var cts = new CancellationTokenSource();
        registry.RegisterGraceTimer(Player, cts);

        Assert.True(registry.TryGetGame(Player, out var gameBefore));
        Assert.Equal(Game, gameBefore);

        Assert.True(registry.TryClaimExpiry(Player, cts));

        // Après réclamation de l'éviction, le mapping joueur→partie est purgé.
        Assert.False(registry.TryGetGame(Player, out _));
    }

    [Fact]
    public void TryRemoveConnection_returns_player_and_game_then_keeps_game_mapping()
    {
        var registry = new PlayerConnectionRegistry();
        registry.RegisterConnection(Player, Conn, Game);

        Assert.True(registry.TryRemoveConnection(Conn, out var pid, out var gid));
        Assert.Equal(Player, pid);
        Assert.Equal(Game, gid);

        // La connexion est retirée…
        Assert.False(registry.IsConnected(Player));
        // …mais le mapping partie reste (le timer de grâce en a besoin pour évincer).
        Assert.True(registry.TryGetGame(Player, out _));
    }

    [Fact]
    public async Task AcquirePlayerLockAsync_serializes_critical_sections_per_player()
    {
        var registry = new PlayerConnectionRegistry();

        var first = await registry.AcquirePlayerLockAsync(Player);

        // Une seconde acquisition pour le même joueur doit attendre.
        var second = registry.AcquirePlayerLockAsync(Player);
        Assert.False(second.IsCompleted);

        first.Dispose();

        // Libéré : la seconde acquisition se débloque.
        var secondLock = await second;
        secondLock.Dispose();
    }

    [Fact]
    public async Task AcquirePlayerLockAsync_does_not_block_across_players()
    {
        var registry = new PlayerConnectionRegistry();

        var lockA = await registry.AcquirePlayerLockAsync("player-A");
        // Un autre joueur ne doit pas être bloqué par le verrou de A.
        var lockB = await registry.AcquirePlayerLockAsync("player-B");

        lockA.Dispose();
        lockB.Dispose();
    }
}
