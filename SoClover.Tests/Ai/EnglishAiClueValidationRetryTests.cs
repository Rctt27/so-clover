using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;
using SoClover.Tests.Helpers;
using SoClover.UseCases.AI;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.AI;

/// <summary>
/// Proves the English AI flow is identical to the French one: an LLM clue that conflicts with a board
/// word (R1) is rejected by EnglishOffClueValidator, triggering the retry loop until a valid clue is
/// produced. Uses the REAL prompt provider factory + REAL validator factory.
/// </summary>
public class EnglishAiClueValidationRetryTests
{
    private const string EnglishLanguage = "English_(from_FR_OFF)";

    private static ServiceProvider BuildProvider(IChatClient chatClient)
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
        services.AddSingleton(Options.Create(new LlmOptions { MaxRetries = 2, MaxCallsPerGame = 100 }));
        services.AddSingleton(sp => new GameLlmBudget(
            sp.GetRequiredService<IOptions<LlmOptions>>().Value.MaxCallsPerGame));
        services.AddSingleton<IAiCluePromptProviderFactory, AiCluePromptProviderFactory>();
        services.AddSingleton<IAiClueExplanationStore, InMemoryAiClueExplanationStore>();

        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IGenerateAICluesUseCase, GenerateAIClues.Handler>();

        return services.BuildServiceProvider();
    }

    private static void EnqueueClues(FakeChatClient fake,
        (string Dir, string Clue)[] entries)
    {
        var clues = entries.Select(e => new { direction = e.Dir, clueWord = e.Clue, explanation = "x" }).ToArray();
        fake.Enqueue(JsonSerializer.Serialize(new { clues }));
    }

    [Fact]
    public async Task English_clue_conflicting_with_board_word_is_rejected_then_retried_to_a_valid_clue()
    {
        var fake = new FakeChatClient();
        var sp = BuildProvider(fake);
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();

        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var game = new Game(GameId.New(), EnglishLanguage);
        game.AddPlayer(alice);
        game.AddAIPlayer(bot, max: 4);
        await repo.Save(game);

        // English game must enable semantic validation by default (parity with French).
        Assert.True((await repo.Get(game.Id))!.SemanticClueCheckEnabled);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var botBoard = (await repo.Get(game.Id))!.Players.First(p => p.Id == bot.Id).Board;
        var safe = AiTestHelpers.PickSafeClues(botBoard, 5);

        // A real board word used as the Top clue → R1 ExactMatch → rejected.
        var conflictingWord = botBoard.TopLeft!.Card.TopWord;

        // Response 1: Top conflicts (rejected), the other 3 are safe (accepted).
        EnqueueClues(fake, new[]
        {
            ("Top", conflictingWord),
            ("Right", safe[0]),
            ("Bottom", safe[1]),
            ("Left", safe[2]),
        });
        // Response 2 (retry): only Top remains → a valid clue this time.
        EnqueueClues(fake, new[] { ("Top", safe[3]) });

        var response = await useCase.Handle(new GenerateAIClues.Request(game.Id, bot.Id));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(2, response.LlmCallsConsumed); // initial + 1 retry triggered by the conflict

        var finalBoard = (await repo.Get(game.Id))!.Players.First(p => p.Id == bot.Id).Board;
        Assert.Equal(safe[3], finalBoard.TopClue!.Value.Value);
        Assert.NotNull(finalBoard.RightClue);
        Assert.NotNull(finalBoard.BottomClue);
        Assert.NotNull(finalBoard.LeftClue);
    }
}
