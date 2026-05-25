using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Tests.Helpers;
using SoClover.UseCases.AI;
using SoClover.UseCases.Abstractions;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueOrchestratorHostedServiceTests
{
    [Fact]
    public async Task Processes_enqueued_message_and_calls_GenerateAIClues()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var channel = new AiClueWorkChannel();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp, aiCount: 1);
        var aiPid = aiPids[0];
        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);
        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    safe[0], "x"),
            (Direction.Right,  safe[1], "x"),
            (Direction.Bottom, safe[2], "x"),
            (Direction.Left,   safe[3], "x"),
        });

        var service = new AiClueOrchestratorHostedService(scopeFactory, channel, Microsoft.Extensions.Logging.Abstractions.NullLogger<AiClueOrchestratorHostedService>.Instance);
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(new AiClueGenerationRequested(gameId, aiPid));

        await WaitForAsync(async () =>
        {
            var g = await repo.Get(gameId);
            return g != null && g.Players.First(p => p.Id == aiPid).Board.IsSubmitted;
        }, TimeSpan.FromSeconds(5));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        var events = sp.GetRequiredService<InMemoryEventPublisher>();
        Assert.Equal(4, events.PublishedEvents.OfType<AiClueGenerated>().Count());
    }

    [Fact]
    public async Task Continues_processing_after_an_exception_in_one_message()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var channel = new AiClueWorkChannel();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp, aiCount: 2);
        var repo = sp.GetRequiredService<IGameRepository>();
        var board1 = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPids[1]).Board;

        // First AI: the LLM call throws an exception type that Handle does NOT catch
        // (it only swallows InvalidOperationException/JsonException to retry). It bubbles
        // out of Handle and must be caught by the orchestrator, consuming one fake response.
        fake.EnqueueException(new TimeoutException("boom"));

        // Second AI: valid clues.
        var safe = AiTestHelpers.PickSafeClues(board1, 4);
        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    safe[0], "x"),
            (Direction.Right,  safe[1], "x"),
            (Direction.Bottom, safe[2], "x"),
            (Direction.Left,   safe[3], "x"),
        });

        var service = new AiClueOrchestratorHostedService(scopeFactory, channel, Microsoft.Extensions.Logging.Abstractions.NullLogger<AiClueOrchestratorHostedService>.Instance);
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(new AiClueGenerationRequested(gameId, aiPids[0]));
        await channel.Writer.WriteAsync(new AiClueGenerationRequested(gameId, aiPids[1]));

        await WaitForAsync(async () =>
        {
            var g = await repo.Get(gameId);
            return g != null && g.Players.First(p => p.Id == aiPids[1]).Board.IsSubmitted;
        }, TimeSpan.FromSeconds(5));

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        var g0 = await repo.Get(gameId);
        Assert.False(g0!.Players.First(p => p.Id == aiPids[0]).Board.IsSubmitted);
        Assert.True(g0.Players.First(p => p.Id == aiPids[1]).Board.IsSubmitted);
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds:F1}s.");
    }
}
