using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.AI;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;

namespace SoClover.Tests.AI;

internal static class AiTestProvider
{
    public static ServiceProvider Build(
        IChatClient chatClient,
        int budgetMaxCallsPerGame = 50,
        Func<BoardCluesPromptContext, AiCluePromptBundle>? promptBuild = null,
        double? topP = null,
        int? maxOutputTokens = null,
        double? defaultTemperature = null,
        AiClueGenerationMode generationMode = AiClueGenerationMode.PerBoard,
        bool reasoningEnabled = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<InMemoryEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventPublisher>());
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(_ =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton<IClueValidatorFactory, ClueValidatorFactory>();

        services.AddSingleton(chatClient);
        services.AddSingleton(Options.Create(new LlmOptions
        {
            MaxRetries = 2,
            MaxCallsPerGame = Math.Max(1, budgetMaxCallsPerGame),
            TopP = topP,
            MaxOutputTokens = maxOutputTokens,
            DefaultTemperature = defaultTemperature ?? 0.7,
            GenerationMode = generationMode,
            ReasoningEnabled = reasoningEnabled,
        }));
        services.AddSingleton(sp => new GameLlmBudget(
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.MaxCallsPerGame));
        services.AddSingleton<IAiCluePromptProviderFactory>(_ =>
            new TestInlinePromptProviderFactory("Français_OFF", promptBuild));
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();

        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();

        if (generationMode == AiClueGenerationMode.PerDirection)
            services.AddTransient<IGenerateAICluesUseCase, GenerateAICluesPerDirection.Handler>();
        else
            services.AddTransient<IGenerateAICluesUseCase, GenerateAIClues.Handler>();

        return services.BuildServiceProvider();
    }

    public static ServiceProvider BuildWithLogger(
        IChatClient chatClient,
        ILogger<GenerateAIClues.Handler> logger,
        int budgetMaxCallsPerGame = 50,
        Func<BoardCluesPromptContext, AiCluePromptBundle>? promptBuild = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<InMemoryEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventPublisher>());
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(_ =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(_ => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(_ => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddSingleton<IClueValidatorFactory, ClueValidatorFactory>();

        services.AddSingleton(chatClient);
        services.AddSingleton(Options.Create(new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            DefaultModel = "test-model",
            MaxRetries = 2,
            MaxCallsPerGame = Math.Max(1, budgetMaxCallsPerGame),
        }));
        services.AddSingleton(sp => new GameLlmBudget(
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.MaxCallsPerGame));
        services.AddSingleton<IAiCluePromptProviderFactory>(_ =>
            new TestInlinePromptProviderFactory("Français_OFF", promptBuild));
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();

        services.AddSingleton(logger);

        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IGenerateAICluesUseCase, GenerateAIClues.Handler>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Build a game with 1 human admin + N AI players, run StartWritingPhase
    /// (which places 4 cards on each AI board), and return the AI player ids.
    /// </summary>
    public static async Task<(GameId GameId, IReadOnlyList<PlayerId> AiPids)> SetupGameWithAis(
        ServiceProvider sp, int aiCount = 1)
    {
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var game = new Game(GameId.New(), "Français_OFF");
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(human);

        var aiPids = new List<PlayerId>();
        for (var i = 0; i < aiCount; i++)
        {
            var ai = new Player(PlayerId.New(), $"Bot{i}", isAdmin: false, isAI: true,
                aiConfig: new AIConfig("gpt-4o-mini", 0.7));
            game.AddAIPlayer(ai, max: 4);
            aiPids.Add(ai.Id);
        }
        await repo.Save(game);
        await startWriting.Handle(new StartWritingPhase.Request(game.Id));
        return (game.Id, aiPids);
    }

    public static void EnqueueValidJson(FakeChatClient fake,
        IEnumerable<(Direction Dir, string Clue, string Explanation)> entries,
        TimeSpan? artificialDelay = null)
    {
        var clues = entries.Select(e => new
        {
            direction = e.Dir.ToString(),
            clueWord = e.Clue,
            explanation = e.Explanation,
        }).ToArray();
        var json = JsonSerializer.Serialize(new { clues });
        fake.Enqueue(json, artificialDelay);
    }
}

internal sealed class TestInlinePromptProviderFactory : IAiCluePromptProviderFactory
{
    private readonly IAiCluePromptProvider _inner;

    public TestInlinePromptProviderFactory(string language, Func<BoardCluesPromptContext, AiCluePromptBundle>? build = null)
    {
        _inner = new InlinePromptProvider(
            language,
            build ?? (_ => new AiCluePromptBundle("S", "U", "{}")));
    }

    public IAiCluePromptProvider? GetFor(string language) => _inner;
    public bool IsLanguageSupported(string language) => true;
}
