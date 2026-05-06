using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;

namespace SoClover.Infrastructure;

/// <summary>
/// Decorates the inner event publisher and mirrors domain events to SignalR groups per game.
/// Keeps logic thin: extract GameId if present, then notify the group with a generic message.
/// Clients can refetch the full state via the existing HTTP endpoint.
/// </summary>
public sealed class SignalREventPublisher : IEventPublisher
{
    private readonly InMemoryEventPublisher _inner;
    private readonly IHubContext<SoClover.RealTime.GameHub> _hub;
    private readonly IGetGameStateUseCase _getState;

    public SignalREventPublisher(InMemoryEventPublisher inner,
        IHubContext<SoClover.RealTime.GameHub> hub,
        IGetGameStateUseCase getState)
    {
        _inner = inner;
        _hub = hub;
        _getState = getState;
    }

    public async Task Publish<TEvent>(TEvent evt, CancellationToken ct = default)
    {
        // Always invoke the inner publisher (logs, etc.)
        await _inner.Publish(evt!, ct);

        // Try to extract GameId from the event via pattern matching or reflection
        var gameId = ExtractGameId(evt);
        if (gameId is null)
        {
            return; // nothing to broadcast without a game
        }

        // Optional: sanity check game still exists (ignore exceptions to avoid breaking the flow)
        try
        {
            var state = await _getState.Handle(new GetGameState.Request(gameId.Value), ct);

            // Optimization: In WritingClues phase, we DON'T broadcast the full gameState to everyone.
            // The public state lacks personal secrets (cards) and would overwrite local player data.
            // Clients will instead refetch their own state via the API which includes their secrets.
            object? gameStateToSend = state.Phase == GamePhase.WritingClues ? null : state;

            // Keep payload compact: signal that state changed; clients refetch if needed
            await _hub.Clients.Group($"game-{state.GameId}")
                .SendAsync("GameStateUpdated", new
                {
                    eventType = evt!.GetType().Name,
                    gameId = state.GameId,
                    phase = state.Phase.ToString(),
                    phaseEndsAtUtc = state.PhaseEndsAtUtc,
                    revision = state.Revision,
                    eventData = evt, // Include the event data for targeted updates
                    gameState = gameStateToSend // Include full state only if safe
                }, ct);

            // Special-case: countdown warning messages
            switch (evt)
            {
                case SoClover.UseCases.GameLogics.GuessingCountdownWarning guessWarn:
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = "Countdown over! Moving to next Board in 3s",
                            seconds = guessWarn.SecondsRemaining
                        }, ct);
                    break;
                case SoClover.UseCases.GameLogics.WritingCountdownWarning writeWarn:
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = "Writing clue phase ending in 3s!",
                            seconds = writeWarn.SecondsRemaining
                        }, ct);
                    break;
                case SoClover.UseCases.Gameplay.BoardSubmitted boardSub:
                {
                    var player = state.Players.FirstOrDefault(p => p.PlayerId == boardSub.PlayerId.Value);
                    if (player != null)
                    {
                        await _hub.Clients.Group($"game-{state.GameId}")
                            .SendAsync("ServerNotification", new
                            {
                                type = "info",
                                message = $"<strong>{player.Name}</strong> submitted a board",
                                senderId = boardSub.PlayerId.Value.ToString()
                            }, ct);
                    }
                    break;
                }
                case SoClover.UseCases.GameLogics.PlayerJoined playerJoined:
                {
                    var player = state.Players.FirstOrDefault(p => p.PlayerId == playerJoined.PlayerId.Value);
                    if (player != null)
                    {
                        // Send specific event for client handlers to update UI and notify
                        await _hub.Clients.Group($"game-{state.GameId}")
                            .SendAsync("PlayerJoined", new
                            {
                                playerId = player.PlayerId,
                                playerName = player.Name
                            }, ct);
                    }
                    break;
                }
                case SoClover.UseCases.GameLogics.PlayerLeft playerLeft:
                {
                    // Note: player is already removed from state.Players in the backend
                    // We might need to handle notification differently if we want the name.
                    // For now, at least notify of departure.
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "info",
                            message = "Un joueur a quitté la partie"
                        }, ct);
                    break;
                }
                case SoClover.UseCases.GameLogics.PlayerKicked playerKicked:
                {
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = $"<strong>{playerKicked.PlayerName}</strong> a été retiré de la partie par l'admin"
                        }, ct);

                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("PlayerKicked", new
                        {
                            kickedPlayerId = playerKicked.PlayerId.Value.ToString()
                        }, ct);
                    break;
                }
                case SoClover.UseCases.GameLogics.PlayerDisconnected playerDisconnected:
                {
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = $"<strong>{playerDisconnected.PlayerName}</strong> a été déconnecté"
                        }, ct);
                    break;
                }
                case SoClover.UseCases.GameLogics.GameDeleted:
                {
                    await _hub.Clients.Group($"game-{gameId.Value}")
                        .SendAsync("GameDeleted", new { gameId = gameId.Value }, ct);
                    break;
                }
                case SoClover.UseCases.Gameplay.BoardRotated boardRotated:
                {
                    // Emit specific BoardRotationUpdated event for targeted sync without full state refresh
                    await _hub.Clients.Group($"game-{state.GameId}")
                        .SendAsync("BoardRotationUpdated", new
                        {
                            cumulativeRotation = boardRotated.CumulativeRotation,
                            playerId = boardRotated.PlayerId.Value.ToString(),
                            revision = boardRotated.Revision
                        }, ct);
                    break;
                }
            }
        }
        catch (GameNotFoundException)
        {
            // If the game was just deleted, Handle might throw GameNotFoundException
            if (evt is SoClover.UseCases.GameLogics.GameDeleted)
            {
                await _hub.Clients.Group($"game-{gameId.Value}")
                    .SendAsync("GameDeleted", new { gameId = gameId.Value }, ct);
            }
        }
        catch
        {
            // Swallow to keep domain flow robust
        }
    }

    private static GameId? ExtractGameId<TEvent>(TEvent evt)
    {
        // Common cases: records with property named "GameId" of type GameId
        var type = evt!.GetType();
        var prop = type.GetProperty("GameId", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(GameId))
        {
            var value = (GameId)prop.GetValue(evt)!;
            return value;
        }
        return null;
    }
}
