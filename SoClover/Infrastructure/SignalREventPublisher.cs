using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Games;

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
            // Keep payload compact: signal that state changed; clients refetch if needed
            await _hub.Clients.Group($"game-{state.GameId.Value}")
                .SendAsync("GameStateUpdated", new
                {
                    eventType = evt!.GetType().Name,
                    gameId = state.GameId.Value,
                    phase = state.Phase.ToString(),
                    phaseEndsAtUtc = state.PhaseEndsAtUtc
                }, ct);

            // Special-case: countdown warning messages
            switch (evt)
            {
                case SoClover.UseCases.Games.GuessingCountdownWarning guessWarn:
                    await _hub.Clients.Group($"game-{state.GameId.Value}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = "Countdown over! Moving to next Board in 3s",
                            seconds = guessWarn.SecondsRemaining
                        }, ct);
                    break;
                case SoClover.UseCases.Games.WritingCountdownWarning writeWarn:
                    await _hub.Clients.Group($"game-{state.GameId.Value}")
                        .SendAsync("ServerNotification", new
                        {
                            type = "warning",
                            message = "Writing clue phase ending in 3s!",
                            seconds = writeWarn.SecondsRemaining
                        }, ct);
                    break;
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
