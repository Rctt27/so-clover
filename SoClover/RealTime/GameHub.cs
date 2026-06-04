using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.GameLogics;

namespace SoClover.RealTime;

public sealed class GameHub : Hub
{
    private readonly IGetGameStateUseCase _getState;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly ConcurrentDictionary<string, string> _playerConnections = new(); // playerId -> connectionId
    private static readonly ConcurrentDictionary<string, string> _playerGameMap = new(); // playerId -> gameId
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _disconnectTimers = new();

    public GameHub(IGetGameStateUseCase getState, IServiceScopeFactory scopeFactory)
    {
        _getState = getState;
        _scopeFactory = scopeFactory;
    }

    public static bool IsPlayerConnected(PlayerId playerId) =>
        _playerConnections.ContainsKey(playerId.Value.ToString());

    public async Task JoinGame(string gameId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !Guid.TryParse(playerId, out var pid))
        {
            throw new HubException("Invalid identifiers");
        }

        // Cancel any pending disconnect timer for this player
        if (_disconnectTimers.TryRemove(playerId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            Console.WriteLine($"[GameHub] Cancelled disconnect timer for player {playerId}");
        }

        var response = await _getState.Handle(new GetGameState.Request(GameId.From(gameId)), CancellationToken.None);

        var isMember = response.Players.Any(p => p.PlayerId == pid);
        if (!isMember)
        {
            throw new HubException("Unauthorized: player not in game");
        }

        _playerConnections[playerId] = Context.ConnectionId;
        _playerGameMap[playerId] = gameId;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

        // Réactivation au rejoin : un-mark IsDisconnected si le joueur était marqué
        // déconnecté (no-op hors WritingClues / si non déconnecté, géré par le use case).
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReconnectPlayerUseCase>()
            .Handle(new ReconnectPlayer.Request(GameId.From(gameId), new PlayerId(pid)));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var item = _playerConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (item.Key != null)
        {
            var playerId = item.Key;
            _playerConnections.TryRemove(playerId, out _);

            if (_playerGameMap.TryGetValue(playerId, out var gameId))
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
                _disconnectTimers[playerId] = timerCts;

                Console.WriteLine($"[GameHub] Player {playerId} disconnected. Starting {graceSeconds}s grace period.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(graceSeconds), timerCts.Token);

                        // Grace period expired — player did not reconnect
                        _disconnectTimers.TryRemove(playerId, out _);
                        _playerGameMap.TryRemove(playerId, out _);

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
                    catch (TaskCanceledException)
                    {
                        // Player reconnected — timer was cancelled, nothing to do
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GameHub] Error during grace period handling for {playerId}: {ex.Message}");
                    }
                });
            }
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
