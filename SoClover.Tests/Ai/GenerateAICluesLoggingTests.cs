using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoClover.Domain;
using SoClover.Tests.Helpers;
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
        var safe = AiTestHelpers.PickSafeClues(board, 4);
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
        var safe = AiTestHelpers.PickSafeClues(board, 4);
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

    [Fact]
    public async Task Emits_warning_with_finishReason_and_usage_when_LLM_returns_empty_content()
    {
        // Reproduit le cas gemma : le reasoning natif sature la fenêtre de contexte et la
        // complétion est tronquée (finish_reason=length) AVANT l'émission du JSON → content vide.
        // Sans ce log, l'échec est noyé dans un générique « empty response » non actionnable.
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAIClues.Handler>();

        var sp = AiTestProvider.BuildWithLogger(fake, capturing);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        // PerBoard : maxRetries=2 → 3 tentatives, 1 appel chacune.
        for (var i = 0; i < 3; i++)
            fake.EnqueueResponse(EmptyTruncatedResponse());

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        var emptyWarnings = capturing.Records.Where(r =>
            r.Level == LogLevel.Warning &&
            r.Message.Contains("empty content", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(emptyWarnings);

        var w = emptyWarnings[0];
        Assert.True(w.Properties.ContainsKey("GameId"));
        Assert.True(w.Properties.ContainsKey("PlayerId"));
        Assert.True(w.Properties.ContainsKey("Attempt"));
        Assert.True(w.Properties.ContainsKey("FinishReason"));
        Assert.True(w.Properties.ContainsKey("OutputTokens"));
        Assert.True(w.Properties.ContainsKey("LlmModel"));
    }

    private static ChatResponse EmptyTruncatedResponse() =>
        new(new ChatMessage(ChatRole.Assistant, string.Empty))
        {
            FinishReason = ChatFinishReason.Length,
            Usage = new UsageDetails { InputTokenCount = 1200, OutputTokenCount = 2900 },
        };

    [Fact]
    public async Task Emits_a_recognizable_warning_with_the_raw_text_excerpt_when_the_LLM_returns_invalid_JSON()
    {
        // AiClueResponseParser.ParseBoard encapsule tout JsonException dans
        // UnparseableLlmResponseException (dérive d'InvalidOperationException) : le catch générique
        // "AI clue LLM call failed" gagnerait sur un ancien catch(JsonException) sans cette distinction
        // explicite dans GenerateAIClues.Handler.FillRemainingAsync — ce test verrouille le message
        // de diagnostic distinct et le rawTextExcerpt qui remplacent l'ancien log "unparseable JSON".
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAIClues.Handler>();

        var sp = AiTestProvider.BuildWithLogger(fake, capturing);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        // PerBoard : maxRetries=2 → 3 tentatives, 1 appel chacune.
        for (var i = 0; i < 3; i++)
            fake.Enqueue("pas du JSON du tout");

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        var unparseableWarnings = capturing.Records.Where(r =>
            r.Level == LogLevel.Warning &&
            r.Message.Contains("unparseable JSON", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(unparseableWarnings);

        var w = unparseableWarnings[0];
        Assert.True(w.Properties.ContainsKey("GameId"));
        Assert.True(w.Properties.ContainsKey("PlayerId"));
        Assert.True(w.Properties.ContainsKey("Attempt"));
        Assert.True(w.Properties.ContainsKey("RawTextExcerpt"));
        Assert.Contains("pas du JSON", w.Properties["RawTextExcerpt"] as string);

        // Le générique "AI clue LLM call failed" ne doit plus apparaître pour ce cas précis :
        // il ne couvre désormais que les autres InvalidOperationException (ex. non-UnparseableLlmResponseException).
        var genericWarnings = capturing.Records.Where(r =>
            r.Level == LogLevel.Warning &&
            r.Message.Contains("AI clue LLM call failed", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(genericWarnings);
    }
}
