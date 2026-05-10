using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAIConcurrencyTests
{
    [Fact]
    public async Task Three_AIs_concurrent_with_MaxConcurrency_1_serializes_LLM_calls()
    {
        var fake = new FakeChatClient();
        for (var i = 0; i < 3; i++)
        {
            var json = JsonSerializer.Serialize(new
            {
                clues = new[]
                {
                    new { direction = "Top",    clueWord = $"top{i}",    explanation = "x" },
                    new { direction = "Right",  clueWord = $"right{i}",  explanation = "x" },
                    new { direction = "Bottom", clueWord = $"bottom{i}", explanation = "x" },
                    new { direction = "Left",   clueWord = $"left{i}",   explanation = "x" },
                }
            });
            fake.Enqueue(json, artificialDelay: TimeSpan.FromMilliseconds(100));
        }

        var throttled = new ThrottlingChatClient(fake, maxConcurrency: 1);
        var sp = AiTestProvider.Build(throttled);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp, aiCount: 3);

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();

        var tasks = aiPids
            .Select(pid => useCase.Handle(new GenerateAIClues.Request(gameId, pid)))
            .ToArray();
        await Task.WhenAll(tasks);

        var log = fake.CallLog;
        Assert.Equal(3, log.Count);
        for (var i = 1; i < log.Count; i++)
        {
            Assert.True(log[i].Start >= log[i - 1].End,
                $"Call {i} started ({log[i].Start:O}) before call {i - 1} ended ({log[i - 1].End:O}) " +
                "— throttling failed (MaxConcurrency=1 expected serialization).");
        }
    }
}
