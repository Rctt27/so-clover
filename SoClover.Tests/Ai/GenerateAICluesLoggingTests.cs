using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoClover.Domain;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAICluesLoggingTests
{
    [Fact]
    public async Task Emits_LlmCallCompleted_log_with_structured_properties()
    {
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAIClues.Handler>();

        var sp = AiTestProvider.BuildWithLogger(fake, capturing);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<SoClover.UseCases.Abstractions.IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = GenerateAICluesTests.PickSafeClues(board, 4);
        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    safe[0], "exp"),
            (Direction.Right,  safe[1], "exp"),
            (Direction.Bottom, safe[2], "exp"),
            (Direction.Left,   safe[3], "exp"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        var callLogs = capturing.Records.Where(r =>
            r.Message.Contains("LLM call completed", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(callLogs);
        var log = callLogs[0];
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.True(log.Properties.ContainsKey("GameId"));
        Assert.True(log.Properties.ContainsKey("PlayerId"));
        Assert.True(log.Properties.ContainsKey("Attempt"));
        Assert.True(log.Properties.ContainsKey("LatencyMs"));
        Assert.True(log.Properties.ContainsKey("LlmProvider"));
        Assert.True(log.Properties.ContainsKey("LlmModel"));
        Assert.True(log.Properties.ContainsKey("PromptVersion"));
        Assert.True(log.Properties.ContainsKey("RemainingDirections"));
    }

    [Fact]
    public async Task Emits_ClueValidated_log_per_accepted_direction()
    {
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAIClues.Handler>();

        var sp = AiTestProvider.BuildWithLogger(fake, capturing);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<SoClover.UseCases.Abstractions.IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = GenerateAICluesTests.PickSafeClues(board, 4);
        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    safe[0], "exp"),
            (Direction.Right,  safe[1], "exp"),
            (Direction.Bottom, safe[2], "exp"),
            (Direction.Left,   safe[3], "exp"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        var validated = capturing.Records.Where(r =>
            r.Properties.TryGetValue("IsValid", out var v) && v is bool b && b == true).ToList();
        Assert.Equal(4, validated.Count);
        foreach (var rec in validated)
        {
            Assert.True(rec.Properties.ContainsKey("Direction"));
            Assert.True(rec.Properties.ContainsKey("ClueText"));
            Assert.True(rec.Properties.ContainsKey("PromptVersion"));
            Assert.True(rec.Properties.ContainsKey("LlmProvider"));
            Assert.True(rec.Properties.ContainsKey("LlmModel"));
        }
    }

    [Fact]
    public async Task Emits_ClueRejected_log_with_RejectionRules()
    {
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAIClues.Handler>();

        var sp = AiTestProvider.BuildWithLogger(fake, capturing);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<SoClover.UseCases.Abstractions.IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var invalid = board.TopLeft!.GetWord(Direction.Top);
        AiTestProvider.EnqueueValidJson(fake, new (Direction Dir, string Clue, string Explanation)[]
        {
            (Direction.Top,    invalid, "exp"),
            (Direction.Right,  invalid, "exp"),
            (Direction.Bottom, invalid, "exp"),
            (Direction.Left,   invalid, "exp"),
        });
        for (var i = 0; i < 3; i++) AiTestProvider.EnqueueValidJson(fake, new (Direction Dir, string Clue, string Explanation)[]
        {
            (Direction.Top,    invalid, "exp"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        var rejections = capturing.Records.Where(r =>
            r.Properties.TryGetValue("IsValid", out var v) && v is bool b && b == false).ToList();
        Assert.NotEmpty(rejections);
        Assert.All(rejections, r =>
        {
            Assert.True(r.Properties.ContainsKey("RejectionRules"));
            var rules = r.Properties["RejectionRules"] as string;
            Assert.False(string.IsNullOrWhiteSpace(rules));
        });
    }
}
