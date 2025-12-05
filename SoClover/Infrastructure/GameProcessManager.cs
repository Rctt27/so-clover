using Microsoft.Extensions.Hosting;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Games;
using System.Collections.Concurrent;

namespace SoClover.Infrastructure;

public sealed class GameProcessManager : BackgroundService
{
    private readonly IGameRepository _repo;
    private readonly IClock _clock;
    private readonly IStartGuessingPhaseUseCase _startGuessing;
    private readonly IMoveToNextBoardUseCase _moveNext;
    private readonly IDeleteGameUseCase _deleteGame;
    private readonly ICompleteGameUseCase _completeGame;
    private readonly IEventPublisher _events;

    // Track which countdown warnings have been sent (per game/board/deadline)
    private readonly ConcurrentDictionary<string, byte> _warningSent = new();

    public GameProcessManager(
        IGameRepository repo,
        IClock clock,
        IStartGuessingPhaseUseCase startGuessing,
        IMoveToNextBoardUseCase moveNext,
        IDeleteGameUseCase deleteGame,
        ICompleteGameUseCase completeGame,
        IEventPublisher events)
    {
        _repo = repo;
        _clock = clock;
        _startGuessing = startGuessing;
        _moveNext = moveNext;
        _deleteGame = deleteGame;
        _completeGame = completeGame;
        _events = events;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Simple polling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _clock.UtcNow;
                var games = await _repo.GetAll(stoppingToken);
                foreach (var game in games)
                {
                    if (game.PhaseEndsAtUtc is not DateTime endsAt)
                        continue;

                    // Send a 3s warning during Guessing phase
                    if (game.Phase == GamePhase.Guessing && now < endsAt)
                    {
                        var remaining = endsAt - now;
                        if (remaining <= TimeSpan.FromSeconds(3) && remaining > TimeSpan.Zero)
                        {
                            var key = $"{game.Id.Value}-{game.CurrentGuessingBoardOwner?.Value}-{endsAt:O}";
                            if (_warningSent.TryAdd(key, 1))
                            {
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
                    switch (game.Phase)
                    {
                        case GamePhase.Lobby:
                            // Lobby expired without start -> cancel game
                            await _deleteGame.Handle(new DeleteGame.Request(game.Id), stoppingToken);
                            break;
                        case GamePhase.WritingClues:
                            await _startGuessing.Handle(new StartGuessingPhase.Request(game.Id), stoppingToken);
                            break;
                        case GamePhase.Guessing:
                            // Triggered by system (deadline reached)
                            await _moveNext.Handle(
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
                                await _completeGame.Handle(new CompleteGame.Request(game.Id, adminId), stoppingToken);
                            }
                            break;
                        default:
                            // no-op
                            break;
                    }
                }
            }
            catch
            {
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