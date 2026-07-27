using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.AI.Reasoning;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueLlmCallerTests
{
    private static readonly IReadOnlyList<BoardCardSnapshot> Cards =
    [
        new(BoardPosition.TopLeft,     "Lune",    "Route",  "Plage",    "Ciel"),
        new(BoardPosition.TopRight,    "Vague",   "Rocher", "Sable",    "Île"),
        new(BoardPosition.BottomRight, "Oiseau",  "Forêt",  "Montagne", "Vent"),
        new(BoardPosition.BottomLeft,  "Rivière", "Pont",   "Ville",    "Village"),
    ];

    private static ClueCallRequest Request(
        string? modelOverride = null, double? temperatureOverride = null) =>
        new("Français_OFF", Cards, [Direction.Top],
            new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>(),
            modelOverride, temperatureOverride);

    private static AiClueLlmCaller Build(
        FakeChatClient chat,
        Action<LlmOptions>? configure = null,
        IReasoningRequestConfigurator? reasoning = null)
    {
        var opts = new LlmOptions
        {
            DefaultModel = "default-model",
            DefaultTemperature = 0.7,
            ReasoningEnabled = false,
        };
        configure?.Invoke(opts);
        return new AiClueLlmCaller(chat, Options.Create(opts), reasoning);
    }

    private static AiCluePromptBundle Bundle(BoardCluesPromptContext ctx) =>
        new("SYSTEM TEXT", "USER TEXT", "{}", PromptVersion: 5);

    private static Task<ClueCallResult> Call(
        AiClueLlmCaller caller, ClueCallRequest? request = null) =>
        caller.CallAsync(
            request ?? Request(),
            new InlinePromptProvider("Français_OFF", _ => Bundle(default)),
            static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
            AiClueResponseParser.ParseSingleDirection,
            CancellationToken.None);

    [Fact]
    public async Task Returns_draft_prompt_version_effective_model_and_latency()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");

        var result = await Call(Build(chat));

        Assert.Equal("Rivage", result.Draft.Clues[0].ClueWord);
        Assert.Equal(5, result.PromptVersion);
        Assert.Equal("default-model", result.EffectiveModel);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task Model_and_temperature_overrides_win_over_the_defaults()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");

        var result = await Call(Build(chat), Request(modelOverride: "player-model", temperatureOverride: 1.0));

        Assert.Equal("player-model", chat.LastOptions!.ModelId);
        Assert.Equal(1.0f, chat.LastOptions.Temperature);
        Assert.Equal("player-model", result.EffectiveModel);
    }

    [Fact]
    public async Task TopP_and_MaxOutputTokens_are_applied_only_when_set()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");

        await Call(Build(chat, o => { o.TopP = 0.95; o.MaxOutputTokens = 4096; }));

        Assert.Equal(0.95f, chat.LastOptions!.TopP);
        Assert.Equal(4096, chat.LastOptions.MaxOutputTokens);

        var chat2 = new FakeChatClient();
        chat2.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");

        await Call(Build(chat2));

        Assert.Null(chat2.LastOptions!.TopP);
        Assert.Null(chat2.LastOptions.MaxOutputTokens);
    }

    [Fact]
    public async Task Empty_response_throws_EmptyLlmResponseException_carrying_finish_reason_and_usage()
    {
        var chat = new FakeChatClient();
        chat.EnqueueResponse(new ChatResponse(new ChatMessage(ChatRole.Assistant, ""))
        {
            FinishReason = ChatFinishReason.Length,
            Usage = new UsageDetails { InputTokenCount = 2100, OutputTokenCount = 4096 },
        });

        var ex = await Assert.ThrowsAsync<EmptyLlmResponseException>(() => Call(Build(chat)));

        Assert.Equal(ChatFinishReason.Length, ex.FinishReason);
        Assert.Equal(2100, ex.InputTokens);
        Assert.Equal(4096, ex.OutputTokens);
        Assert.Equal(5, ex.PromptVersion);
        Assert.Equal("default-model", ex.EffectiveModel);
        Assert.True(ex.LatencyMs >= 0);
    }

    [Fact]
    public async Task Invalid_json_throws_UnparseableLlmResponseException_carrying_observability()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("pas du JSON du tout");

        var ex = await Assert.ThrowsAsync<UnparseableLlmResponseException>(() => Call(Build(chat)));

        Assert.Contains("pas du JSON", ex.RawTextExcerpt);
        Assert.Equal(5, ex.PromptVersion);
        Assert.Equal("default-model", ex.EffectiveModel);
    }

    [Theory]
    [InlineData("<think>réflexion interne</think>{\"direction\":\"Top\",\"clueWord\":\"Rivage\",\"explanation\":\"x\"}")]
    [InlineData("[THINK]bla[/THINK]{\"direction\":\"Top\",\"clueWord\":\"Rivage\",\"explanation\":\"x\"}")]
    [InlineData("```json\n{\"direction\":\"Top\",\"clueWord\":\"Rivage\",\"explanation\":\"x\"}\n```")]
    public async Task Strips_think_tags_and_json_fences_before_parsing(string raw)
    {
        var chat = new FakeChatClient();
        chat.Enqueue(raw);

        var result = await Call(Build(chat));

        Assert.Equal("Rivage", result.Draft.Clues[0].ClueWord);
        Assert.Equal(raw, result.RawText);
    }

    [Fact]
    public async Task Reasoning_configurator_is_invoked_only_when_reasoning_is_enabled()
    {
        var spy = new SpyReasoningConfigurator();

        var chatOff = new FakeChatClient();
        chatOff.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");
        await Call(Build(chatOff, reasoning: spy));
        Assert.Equal(0, spy.Invocations);

        var chatOn = new FakeChatClient();
        chatOn.Enqueue("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");
        await Call(Build(chatOn, o => o.ReasoningEnabled = true, spy));
        Assert.Equal(1, spy.Invocations);
    }

    [Fact]
    public async Task Reasoning_preamble_file_is_prefixed_to_the_system_prompt()
    {
        var preamblePath = Path.Combine(Path.GetTempPath(), $"preamble-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(preamblePath, "PRÉAMBULE MODÈLE");
        try
        {
            var spy = new CapturingChatClient("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");
            var caller = new AiClueLlmCaller(spy, Options.Create(new LlmOptions
            {
                DefaultModel = "default-model",
                ReasoningEnabled = true,
                ReasoningSystemPromptPathEnabler = preamblePath,
            }));

            await caller.CallAsync(Request(), new InlinePromptProvider("Français_OFF", _ => Bundle(default)),
                static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
                AiClueResponseParser.ParseSingleDirection, CancellationToken.None);

            Assert.StartsWith("PRÉAMBULE MODÈLE", spy.LastSystemPrompt);
            Assert.Contains("SYSTEM TEXT", spy.LastSystemPrompt!);
        }
        finally
        {
            if (File.Exists(preamblePath)) File.Delete(preamblePath);
        }
    }

    // Le cache est indexé par (path, LastWriteTimeUtc) : le second appel ne relit pas le disque,
    // ce que l'on prouve en supprimant le fichier entre les deux appels.
    [Fact]
    public async Task Reasoning_preamble_is_cached_by_last_write_time()
    {
        var preamblePath = Path.Combine(Path.GetTempPath(), $"preamble-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(preamblePath, "PRÉAMBULE MODÈLE");

        var spy = new CapturingChatClient("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");
        var caller = new AiClueLlmCaller(spy, Options.Create(new LlmOptions
        {
            DefaultModel = "default-model",
            ReasoningEnabled = true,
            ReasoningSystemPromptPathEnabler = preamblePath,
        }));

        var provider = new InlinePromptProvider("Français_OFF", _ => Bundle(default));
        await caller.CallAsync(Request(), provider,
            static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
            AiClueResponseParser.ParseSingleDirection, CancellationToken.None);

        File.Delete(preamblePath);

        await caller.CallAsync(Request(), provider,
            static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
            AiClueResponseParser.ParseSingleDirection, CancellationToken.None);

        Assert.StartsWith("PRÉAMBULE MODÈLE", spy.LastSystemPrompt);
    }

    [Fact]
    public async Task A_missing_preamble_file_is_reported_and_does_not_abort_the_call()
    {
        var spy = new CapturingChatClient("""{"direction":"Top","clueWord":"Rivage","explanation":"x"}""");
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.txt");
        var caller = new AiClueLlmCaller(spy, Options.Create(new LlmOptions
        {
            DefaultModel = "default-model",
            ReasoningEnabled = true,
            ReasoningSystemPromptPathEnabler = missing,
        }));

        var result = await caller.CallAsync(Request(), new InlinePromptProvider("Français_OFF", _ => Bundle(default)),
            static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
            AiClueResponseParser.ParseSingleDirection, CancellationToken.None);

        Assert.Equal("Rivage", result.Draft.Clues[0].ClueWord);
        Assert.Equal(missing, caller.LastPreambleWarning);
        Assert.StartsWith("SYSTEM TEXT", spy.LastSystemPrompt);
    }

    private sealed class SpyReasoningConfigurator : IReasoningRequestConfigurator
    {
        public int Invocations { get; private set; }
        public void Configure(ChatOptions options) => Invocations++;
    }

    /// <summary>Client de test qui capture le system prompt réellement envoyé.</summary>
    private sealed class CapturingChatClient : IChatClient
    {
        private readonly string _response;
        public CapturingChatClient(string response) => _response = response;
        public string? LastSystemPrompt { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastSystemPrompt = messages.First().Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
