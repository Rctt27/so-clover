using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using System.Collections.Concurrent;

namespace SoClover.Infrastructure;

/// <summary>
/// Background service that manages game phase timeouts.
/// Uses IServiceScopeFactory to properly consume scoped services (like EfGameRepository)
/// from a singleton background service.
/// </summary>
public sealed class GameProcessManager : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly IEventPublisher _events;

    // Track which countdown warnings have been sent (per game/board/deadline)
    private readonly ConcurrentDictionary<string, byte> _warningSent = new();

    public GameProcessManager(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IEventPublisher events)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _events = events;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Simple polling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a new scope for each iteration to get fresh scoped services
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
                var startGuessing = scope.ServiceProvider.GetRequiredService<IStartGuessingPhaseUseCase>();
                var moveNext = scope.ServiceProvider.GetRequiredService<IMoveToNextBoardUseCase>();
                var deleteGame = scope.ServiceProvider.GetRequiredService<IDeleteGameUseCase>();
                var completeGame = scope.ServiceProvider.GetRequiredService<ICompleteGameUseCase>();

                var now = _clock.UtcNow;
                var games = await repo.GetAll(stoppingToken);
                foreach (var game in games)
                {
                    try
                    {
                        if (game.PhaseEndsAtUtc is not DateTime endsAt)
                            continue;

                        if (game.Phase == GamePhase.Guessing && now < endsAt)
                        {
                            var remaining = endsAt - now;
                            if (remaining <= TimeSpan.FromSeconds(3) && remaining > TimeSpan.Zero)
                            {
                                var ownerId = game.CurrentGuessingBoardOwner;
                                var key = $"{game.Id.Value}-{ownerId?.Value}-{endsAt:O}";
                                if (_warningSent.TryAdd(key, 1))
                                {
                                    Console.WriteLine($"[DEBUG_LOG] GameProcessManager: Sending countdown warning for game {game.Id.Value}, board owner {ownerId?.Value}");
                                    // Fire-and-forget event; if it fails, next loop won't resend due to key
                                    await _events.Publish(new GuessingCountdownWarning(game.Id, 3), stoppingToken);
                                }
                            }
                        }

                        // Send a 3s warning during WritingClues phase
                        if (game.Phase == GamePhase.WritingClues && now < endsAt)
                        {
                            var remaining = endsAt - now;
                            if (remaining <= TimeSpan.FromSeconds(3) && remaining > TimeSpan.Zero)
                            {
                                var key = $"{game.Id.Value}-writing-{endsAt:O}";
                                if (_warningSent.TryAdd(key, 1))
                                {
                                    await _events.Publish(new WritingCountdownWarning(game.Id, 3), stoppingToken);
                                }
                            }
                        }

                        if (now < endsAt)
                            continue;

                        // Deadline reached
                        Console.WriteLine($"[DEBUG_LOG] GameProcessManager: Deadline reached for game {game.Id.Value} in phase {game.Phase}. EndsAt={endsAt:O}, Now={now:O}");

                        // Avant de traiter, on vérifie si le jeu a été modifié très récemment en DB
                        // pour éviter de traiter une deadline qui vient d'être traitée par une autre instance
                        // ou un appel client qui a fait avancer le jeu.
                        // On re-fetch le jeu pour avoir son état le plus actuel.
                        var latestGame = await repo.Get(game.Id, stoppingToken);
                        if (latestGame == null || latestGame.Phase != game.Phase || latestGame.PhaseEndsAtUtc != game.PhaseEndsAtUtc)
                        {
                            Console.WriteLine($"[DEBUG_LOG] GameProcessManager: Skipping stale deadline for game {game.Id.Value}. Current Phase={latestGame?.Phase}, EndsAt={latestGame?.PhaseEndsAtUtc:O}");
                            continue;
                        }

                        switch (game.Phase)
                        {
                            case GamePhase.Lobby:
                                // Lobby expired without start -> cancel game
                                await deleteGame.Handle(new DeleteGame.Request(game.Id), stoppingToken);
                                break;
                            case GamePhase.WritingClues:
                                // Force transition when writing timer hits zero, even if some boards are incomplete/not submitted
                                await startGuessing.Handle(new StartGuessingPhase.Request(game.Id, true), stoppingToken);
                                break;
                            case GamePhase.Guessing:
                                // Triggered by system (deadline reached)
                                await moveNext.Handle(
                                    new MoveToNextBoard.Request(
                                        game.Id,
                                        game.CurrentGuessingBoardOwner ?? default,
                                        SoClover.UseCases.Abstractions.InvocationOrigin.System
                                    ),
                                    stoppingToken);
                                break;
                            case GamePhase.Scoring:
                                if (game.AdminPlayerId is PlayerId adminId)
                                {
                                    await completeGame.Handle(new CompleteGame.Request(game.Id, adminId), stoppingToken);
                                }
                                break;
                            default:
                                // no-op
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG_LOG] GameProcessManager ERROR processing game {game.Id.Value}: {ex.Message}");
                        // Continue to next game
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG_LOG] GameProcessManager CRITICAL ERROR in main loop: {ex.Message}");
                // swallow errors to keep background loop alive; could log in a real logger
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }
    }
}
