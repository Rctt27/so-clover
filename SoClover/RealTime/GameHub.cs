using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.GameLogics;

namespace SoClover.RealTime;

public sealed class GameHub : Hub
{
    private readonly IGetGameStateUseCase _getState;
    private static readonly ConcurrentDictionary<string, string> _playerConnections = new(); // playerId -> connectionId

    public GameHub(IGetGameStateUseCase getState)
    {
        _getState = getState;
    }

    public async Task JoinGame(string gameId, string playerId)
    {
        if (!Guid.TryParse(gameId, out var gid) || !Guid.TryParse(playerId, out var pid))
        {
            throw new HubException("Invalid identifiers");
        }

        var response = await _getState.Handle(new GetGameState.Request(new GameId(gid)), CancellationToken.None);

        var isMember = response.Players.Any(p => p.PlayerId == pid);
        if (!isMember)
        {
            throw new HubException("Unauthorized: player not in game");
        }

        _playerConnections[playerId] = Context.ConnectionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

        // Optionally, send current state to caller (clients can also fetch via HTTP)
        // await Clients.Caller.SendAsync("GameStateUpdated", MapState(response));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var item = _playerConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (item.Key != null)
        {
            _playerConnections.TryRemove(item.Key, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(string gameId) => $"game-{gameId}";

    // Server-side throttle for mouse moves (per player per game)
    private static readonly ConcurrentDictionary<(string gameId, string playerId), DateTime> _lastMouse = new();
    private static readonly TimeSpan MouseRate = TimeSpan.FromMilliseconds(25); // 40 Hz max (réduit de 50ms pour tolérer les envois plus fréquents du worker)

    public class MouseMoveDto
    {
        public double NX { get; set; }
        public double NY { get; set; }
        public long T { get; set; }
    }

    public async Task SendMousePositions(string gameId, string playerId, List<MouseMoveDto> positions)
    {
        // Basic validation
        if (!Guid.TryParse(gameId, out var gid) || !Guid.TryParse(playerId, out var pid) || positions == null || positions.Count == 0)
        {
            return; // ignore invalid
        }

        // Throttle (still applied to the whole batch to avoid spam)
        var key = (gameId, playerId);
        var now = DateTime.UtcNow;
        var last = _lastMouse.GetOrAdd(key, DateTime.MinValue);
        if (now - last < MouseRate) return;
        _lastMouse[key] = now;

        // Console.WriteLine($"[DEBUG_LOG] SendMousePositions from {playerId} in game {gameId}. Count: {positions.Count}");

        // Validate membership and phase
        var state = await _getState.Handle(new GetGameState.Request(new GameId(gid)), CancellationToken.None);
        var player = state.Players.FirstOrDefault(p => p.PlayerId == pid);
        if (player is null) return; // not part of game
        if (state.Phase != GamePhase.Guessing || state.GuessingState is null) return; // only during GuessingPhase

        var excludedIds = new List<string> { Context.ConnectionId };
        
        await Clients.GroupExcept(GroupName(gameId), excludedIds).SendAsync("GuessingMouseMoved", new
        {
            playerId,
            playerName = player.Name,
            positions
        });
    }

    public async Task BroadcastBoardRotation(string gameId, int cumulativeRotation)
    {
        if (!Guid.TryParse(gameId, out _)) return;
        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("BoardRotationUpdated", new { cumulativeRotation });
    }
}
