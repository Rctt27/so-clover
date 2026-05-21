using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoClover.UseCases.AI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// BackgroundService that drains the AiClueWorkChannel and dispatches each
/// AiClueGenerationRequested message to IGenerateAICluesUseCase via a fresh scope.
/// Pattern mirrors GameProcessManager: scope-per-iteration so scoped services
/// (e.g. EfGameRepository in RELEASE) resolve correctly.
/// Errors are swallowed and logged so a single bad message cannot kill the service.
/// </summary>
public sealed class AiClueOrchestratorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AiClueWorkChannel _channel;
    private readonly ILogger<AiClueOrchestratorHostedService> _logger;

    public AiClueOrchestratorHostedService(
        IServiceScopeFactory scopeFactory,
        AiClueWorkChannel channel,
        ILogger<AiClueOrchestratorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var msg in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider
                        .GetRequiredService<IGenerateAICluesUseCase>();
                    await useCase.Handle(
                        new GenerateAIClues.Request(msg.GameId, msg.PlayerId),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "AI clue generation error: game={GameId} player={PlayerId}",
                        msg.GameId, msg.PlayerId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }
}
