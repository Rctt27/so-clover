using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAICluesPerDirectionTests
{
    private static ServiceProvider BuildPerDirection(
        FakeChatClient chat,
        int budgetMaxCallsPerGame = 50,
        Func<BoardCluesPromptContext, AiCluePromptBundle>? promptBuild = null,
        bool reasoningEnabled = false)
    {
        return AiTestProvider.Build(
            chatClient: chat,
            budgetMaxCallsPerGame: budgetMaxCallsPerGame,
            promptBuild: promptBuild,
            generationMode: AiClueGenerationMode.PerDirection,
            reasoningEnabled: reasoningEnabled);
    }

    [Fact]
    public async Task HappyPath_four_calls_one_direction_each_emits_4_AiClueGenerated_and_auto_submits()
    {
        var fake = new FakeChatClient();
        var sp = BuildPerDirection(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);

        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top,    safe[0], "exp top") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  safe[1], "exp right") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[2], "exp bottom") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[3], "exp left") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(4, response.LlmCallsConsumed);

        var generated = events.PublishedEvents.OfType<AiClueGenerated>().ToList();
        Assert.Equal(4, generated.Count);

        var game = await repo.Get(gameId);
        Assert.True(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task Retry_one_direction_first_attempt_invalid_second_attempt_valid()
    {
        var fake = new FakeChatClient();
        var sp = BuildPerDirection(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);
        var conflict = PickConflictWord(board);

        // Top OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top, safe[0], "ok") });
        // Right : 1re tentative invalide (mot du board), 2e OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, conflict, "conflit") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, safe[1], "ok") });
        // Bottom + Left OK
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[2], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[3], "ok") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(5, response.LlmCallsConsumed); // 4 directions + 1 retry sur Right

        var generated = events.PublishedEvents.OfType<AiClueGenerated>().ToList();
        Assert.Equal(4, generated.Count);

        var game = await repo.Get(gameId);
        Assert.True(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task PartialExhaustion_two_directions_fail_after_max_retries_no_auto_submit()
    {
        var fake = new FakeChatClient();
        var sp = BuildPerDirection(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 2);
        var conflict = PickConflictWord(board);

        // Top OK, Right : 3 tentatives invalides, Bottom OK, Left : 3 tentatives invalides.
        // MaxRetries=2 (cf. AiTestProvider) → maxAttempts=3 par direction.
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top, safe[0], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, conflict, "c1") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, conflict, "c2") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, conflict, "c3") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[1], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left, conflict, "c1") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left, conflict, "c2") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left, conflict, "c3") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(2, response.SucceededCount);
        Assert.Equal(2, response.FailedCount);
        Assert.Equal(8, response.LlmCallsConsumed);

        // Events finaux émis par la base pour Right et Left
        var failed = events.PublishedEvents.OfType<AiClueGenerationFailed>().ToList();
        Assert.Contains(failed, e => e.Direction == Direction.Right);
        Assert.Contains(failed, e => e.Direction == Direction.Left);
        Assert.Contains(events.PublishedEvents.OfType<AiPlayerBoardFailed>(), _ => true);

        // Pas d'auto-submit
        var game = await repo.Get(gameId);
        Assert.False(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task BudgetExhausted_midboard_persists_resolved_directions_and_fails_remaining()
    {
        var fake = new FakeChatClient();
        // Budget = 2 → 2 directions résolues, puis LlmBudgetExhaustedException sur la 3e.
        var sp = BuildPerDirection(fake, budgetMaxCallsPerGame: 2);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 2);

        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top,   safe[0], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, safe[1], "ok") });
        // pas besoin d'enfiler plus : ConsumeBudget va lever avant le 3e appel

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(2, response.SucceededCount);
        Assert.Equal(2, response.FailedCount);
        Assert.Equal(2, response.LlmCallsConsumed); // 2 appels consommés, le 3e a levé avant l'incrément

        // Top et Right ont été persistés
        var game = await repo.Get(gameId);
        var savedBoard = game!.Players.First(p => p.Id == aiPid).Board;
        Assert.NotNull(savedBoard.TopClue);
        Assert.NotNull(savedBoard.RightClue);
        Assert.Null(savedBoard.BottomClue);
        Assert.Null(savedBoard.LeftClue);
        Assert.False(savedBoard.IsSubmitted);

        // Events budget
        var failed = events.PublishedEvents.OfType<AiClueGenerationFailed>().ToList();
        Assert.Contains(failed, e => e.Direction == Direction.Bottom && e.Reason.Contains("budget"));
        Assert.Contains(failed, e => e.Direction == Direction.Left   && e.Reason.Contains("budget"));
    }

    [Fact]
    public async Task EachCall_carries_exactly_one_remaining_direction()
    {
        var fake = new FakeChatClient();
        var capturedRemaining = new List<int>();
        var sp = BuildPerDirection(fake, promptBuild: ctx =>
        {
            capturedRemaining.Add(ctx.RemainingDirections.Count);
            return new AiCluePromptBundle("S", "U", "{}");
        });
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 4);

        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top,    safe[0], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  safe[1], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[2], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[3], "ok") });

        await sp.GetRequiredService<IGenerateAICluesUseCase>()
            .Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, capturedRemaining.Count);
        Assert.All(capturedRemaining, c => Assert.Equal(1, c));
    }

    private static string PickConflictWord(CloverBoard board)
    {
        // Réutilise le pattern du test PerBoard : prend un mot apparaissant déjà sur le board pour forcer un rejet.
        return board.TopLeft!.GetWord(Direction.Top);
    }
}
