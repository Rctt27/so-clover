using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.GameLogics;

namespace SoClover.RealTime;

public sealed class GameHub : Hub
{
    private readonly IGetGameStateUseCase _getState;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlayerConnectionRegistry _registry;

    public GameHub(IGetGameStateUseCase getState, IServiceScopeFactory scopeFactory, PlayerConnectionRegistry registry)
    {
        _getState = getState;
        _scopeFactory = scopeFactory;
        _registry = registry;
    }

    public async Task JoinGame(string gameId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !Guid.TryParse(playerId, out var pid))
        {
            throw new HubException("Invalid identifiers");
        }

        // Sérialise rejoin vs expiration de grâce pour ce joueur : la vérification d'appartenance
        // et le ré-enregistrement ci-dessous ne peuvent pas s'entrelacer avec une éviction concurrente.
        using var _ = await _registry.AcquirePlayerLockAsync(playerId);

        // Le rejoin gagne la course contre le timer de grâce : on l'annule.
        _registry.CancelGraceTimer(playerId);
        Console.WriteLine($"[GameHub] Cancelled disconnect timer for player {playerId}");

        GetGameState.Response response;
        try
        {
            response = await _getState.Handle(new GetGameState.Request(GameId.From(gameId)), CancellationToken.None);
        }
        catch (GameNotFoundException)
        {
            // Partie supprimée pendant la grâce (dernier joueur parti) : l'identité persistée
            // côté client est périmée. On normalise en "Unauthorized" pour que le client purge
            // son identité (resetAuth) plutôt que de rester sur un écran mort.
            throw new HubException("Unauthorized: game no longer exists");
        }

        var isMember = response.Players.Any(p => p.PlayerId == pid);
        if (!isMember)
        {
            throw new HubException("Unauthorized: player not in game");
        }

        _registry.RegisterConnection(playerId, Context.ConnectionId, gameId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

        // Réactivation au rejoin : un-mark IsDisconnected si le joueur était marqué
        // déconnecté (no-op hors WritingClues / si non déconnecté, géré par le use case).
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReconnectPlayerUseCase>()
            .Handle(new ReconnectPlayer.Request(GameId.From(gameId), new PlayerId(pid)));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_registry.TryRemoveConnection(Context.ConnectionId, out var playerId, out var gameId))
        {
            // Durée de grâce déterminée par la phase au moment de la déconnexion
            // (plus longue en Lobby pour le mobile). Repli : grâce de jeu.
            int graceSeconds = GracePeriodPolicy.InGameGraceSeconds;
            try
            {
                var snapshot = await _getState.Handle(
                    new GetGameState.Request(GameId.From(gameId)), CancellationToken.None);
                graceSeconds = GracePeriodPolicy.SecondsForPhase(snapshot.Phase);
            }
            catch
            {
                // Partie introuvable / erreur : on garde le repli.
            }

            var timerCts = new CancellationTokenSource();
            _registry.RegisterGraceTimer(playerId, timerCts);

            Console.WriteLine($"[GameHub] Player {playerId} disconnected. Starting {graceSeconds}s grace period.");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(graceSeconds), timerCts.Token);

                    // Sérialise avec un rejoin concurrent : la décision d'évincer et la mutation
                    // domaine ne peuvent pas s'entrelacer avec la vérification d'appartenance du rejoin.
                    using var lk = await _registry.AcquirePlayerLockAsync(playerId);

                    // N'évince que si ce timer est toujours le timer courant ET que le joueur
                    // n'est pas reconnecté. Ferme la race expiration↔reconnexion (bug lobby mobile).
                    if (!_registry.TryClaimExpiry(playerId, timerCts))
                    {
                        Console.WriteLine($"[GameHub] Grace expiry for {playerId} aborted — player reconnected or timer superseded.");
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
                    var game = await repo.Get(GameId.From(gameId));
                    if (game == null) return;

                    var pid = new PlayerId(Guid.Parse(playerId));
                    var action = DisconnectGraceDecision.Decide(game.Phase, game.IsAdmin(pid));

                    Console.WriteLine($"[GameHub] Grace period expired for player {playerId}. Phase={game.Phase}, action={action}.");

                    switch (action)
                    {
                        case GraceAction.LeaveGame:
                            await scope.ServiceProvider.GetRequiredService<ILeaveGameUseCase>()
                                .Handle(new LeaveGame.Request(GameId.From(gameId), pid));
                            break;
                        case GraceAction.DisconnectPlayer:
                            await scope.ServiceProvider.GetRequiredService<IDisconnectPlayerUseCase>()
                                .Handle(new DisconnectPlayer.Request(GameId.From(gameId), pid));
                            break;
                        case GraceAction.None:
                        default:
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Player reconnected — timer was cancelled, nothing to do
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameHub] Error during grace period handling for {playerId}: {ex.Message}");
                }
                finally
                {
                    timerCts.Dispose();
                }
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(string gameId) => $"game-{gameId}";

    // Server-side throttle for mouse moves (per player per game)
    private static readonly ConcurrentDictionary<(string gameId, string playerId), DateTime> _lastMouse = new();
    private static readonly TimeSpan MouseRate = TimeSpan.FromMilliseconds(25);

    public class MouseMoveDto
    {
        public double NX { get; set; }
        public double NY { get; set; }
        public long T { get; set; }
    }

    public async Task SendMousePositions(string gameId, string playerId, List<MouseMoveDto> positions)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !Guid.TryParse(playerId, out var pid) || positions == null || positions.Count == 0)
        {
            return;
        }

        var key = (gameId, playerId);
        var now = DateTime.UtcNow;
        var last = _lastMouse.GetOrAdd(key, DateTime.MinValue);
        if (now - last < MouseRate) return;
        _lastMouse[key] = now;

        var state = await _getState.Handle(new GetGameState.Request(GameId.From(gameId)), CancellationToken.None);
        var player = state.Players.FirstOrDefault(p => p.PlayerId == pid);
        if (player is null) return;
        if (state.Phase != GamePhase.Guessing || state.GuessingState is null) return;

        var excludedIds = new List<string> { Context.ConnectionId };

        await Clients.GroupExcept(GroupName(gameId), excludedIds).SendAsync("GuessingMouseMoved", new
        {
            playerId,
            playerName = player.Name,
            cursorColorIndex = player.CursorColorIndex,
            positions
        });
    }

    public async Task BroadcastBoardRotation(string gameId, int cumulativeRotation)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return;
        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("BoardRotationUpdated", new { cumulativeRotation });
    }
}
