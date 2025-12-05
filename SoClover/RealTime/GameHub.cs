using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.Games;

namespace SoClover.RealTime;

public sealed class GameHub : Hub
{
    private readonly IGetGameStateUseCase _getState;

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

        var isMember = response.Players.Any(p => p.PlayerId.Value == pid);
        if (!isMember)
        {
            throw new HubException("Unauthorized: player not in game");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

        // Optionally, send current state to caller (clients can also fetch via HTTP)
        // await Clients.Caller.SendAsync("GameStateUpdated", MapState(response));
    }

    private static string GroupName(string gameId) => $"game-{gameId}";

    // Server-side throttle for mouse moves (per player per game)
    private static readonly ConcurrentDictionary<(string gameId, string playerId), DateTime> _lastMouse = new();
    private static readonly TimeSpan MouseRate = TimeSpan.FromMilliseconds(50); // 20 Hz max

    public async Task SendMousePosition(string gameId, string playerId, int x, int y)
    {
        // Basic validation
        if (!Guid.TryParse(gameId, out var gid) || !Guid.TryParse(playerId, out var pid))
        {
            return; // ignore invalid
        }

        // Throttle
        var key = (gameId, playerId);
        var now = DateTime.UtcNow;
        var last = _lastMouse.GetOrAdd(key, DateTime.MinValue);
        if (now - last < MouseRate) return;
        _lastMouse[key] = now;

        // Validate membership and phase
        var state = await _getState.Handle(new GetGameState.Request(new GameId(gid)), CancellationToken.None);
        var player = state.Players.FirstOrDefault(p => p.PlayerId.Value == pid);
        if (player is null) return; // not part of game
        if (state.Phase != GamePhase.Guessing || state.GuessingState is null) return; // only during GuessingPhase

        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("GuessingMouseMoved", new
        {
            playerId,
            playerName = player.Name,
            x,
            y
        });
    }
}
