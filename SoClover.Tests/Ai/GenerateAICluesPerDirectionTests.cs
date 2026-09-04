using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    [Fact]
    public async Task ReasoningDisabled_propagates_IncludeReasoning_false_to_prompt_context()
    {
        var fake = new FakeChatClient();
        var capturedIncludeReasoning = new List<bool>();
        var sp = BuildPerDirection(
            fake,
            promptBuild: ctx =>
            {
                capturedIncludeReasoning.Add(ctx.IncludeReasoning);
                return new AiCluePromptBundle("S", "U", "{}");
            },
            reasoningEnabled: false);
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

        Assert.Equal(4, capturedIncludeReasoning.Count);
        Assert.All(capturedIncludeReasoning, v => Assert.False(v));
    }

    [Fact]
    public async Task ReasoningEnabled_propagates_IncludeReasoning_true_to_prompt_context()
    {
        var fake = new FakeChatClient();
        var capturedIncludeReasoning = new List<bool>();
        var sp = BuildPerDirection(
            fake,
            promptBuild: ctx =>
            {
                capturedIncludeReasoning.Add(ctx.IncludeReasoning);
                return new AiCluePromptBundle("S", "U", "{}");
            },
            reasoningEnabled: true);
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

        Assert.Equal(4, capturedIncludeReasoning.Count);
        Assert.All(capturedIncludeReasoning, v => Assert.True(v));
    }

    // Miroir de GenerateAICluesLoggingTests.Emits_a_recognizable_warning_with_the_raw_text_excerpt_when_the_LLM_returns_invalid_JSON
    // (PerBoard), pour le pipeline PerDirection : même diagnostic attendu, plus le champ direction={Direction}
    // que seul PerDirection connaît. Verrouille la correction du Finding Important #1 (le catch de
    // GenerateAICluesPerDirection.FillRemainingAsync ne distinguait plus UnparseableLlmResponseException et
    // perdait le RawTextExcerpt).
    [Fact]
    public async Task Emits_a_recognizable_warning_with_the_raw_text_excerpt_and_direction_when_the_LLM_returns_invalid_JSON()
    {
        var fake = new FakeChatClient();
        var capturing = new CapturingLogger<GenerateAICluesPerDirection.Handler>();

        var sp = AiTestProvider.BuildWithLogger(
            fake, capturing, generationMode: AiClueGenerationMode.PerDirection);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        // PerDirection : maxRetries=2 -> 3 tentatives par direction, 4 directions -> jusqu'à 12 appels.
        for (var i = 0; i < 12; i++)
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
        Assert.True(w.Properties.ContainsKey("Direction"));
        Assert.True(w.Properties.ContainsKey("RawTextExcerpt"));
        Assert.Contains("pas du JSON", w.Properties["RawTextExcerpt"] as string);

        // Le générique "AI clue LLM call failed" ne doit plus apparaître pour ce cas précis.
        var genericWarnings = capturing.Records.Where(r =>
            r.Level == LogLevel.Warning &&
            r.Message.Contains("AI clue LLM call failed", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(genericWarnings);
    }

    // Le pipeline PerDirection restreint volontairement RemainingDirections à la direction courante, mais
    // partageait jusqu'ici l'historique de rejets COMPLET. FileAiCluePromptProvider.ValidateContext porte
    // l'invariant du pipeline PerBoard (« toute direction rejetée est dans les restantes ») et levait donc
    // une ArgumentException dès qu'une direction antérieure avait accumulé un rejet — exception non
    // rattrapée, qui remontait hors de Handle et laissait le board suspendu sans aucun event de fin.
    // Second effet, masqué par le crash : le {{retryFeedback}} du prompt mono-direction rendait aussi
    // les tentatives rejetées des AUTRES directions.
    [Fact]
    public async Task Each_single_direction_call_only_carries_the_rejection_history_of_that_direction()
    {
        var fake = new FakeChatClient();
        var captured = new List<(Direction Resolved, Direction[] RejectedKeys)>();
        var sp = BuildPerDirection(fake, promptBuild: ctx =>
        {
            captured.Add((ctx.RemainingDirections.Single(), ctx.RejectedPerDirection.Keys.ToArray()));
            return new AiCluePromptBundle("S", "U", "{}");
        });
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var board = (await repo.Get(gameId))!.Players.First(p => p.Id == aiPid).Board;
        var safe = AiTestHelpers.PickSafeClues(board, 3);
        var conflict = PickConflictWord(board);

        // Right épuise ses 3 tentatives sur des rejets du validateur : c'est ce qui peuple rejectedHistory
        // avant que Bottom et Left ne soient traitées à leur tour.
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Top,    safe[0], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  conflict, "c1") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  conflict, "c2") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right,  conflict, "c3") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Bottom, safe[1], "ok") });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Left,   safe[2], "ok") });

        await sp.GetRequiredService<IGenerateAICluesUseCase>()
            .Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(6, captured.Count);
        Assert.All(captured, c => Assert.All(c.RejectedKeys, k => Assert.Equal(c.Resolved, k)));

        // Et l'historique reste bien transmis pour la direction courante : la 3e tentative de Right
        // doit voir ses 2 rejets précédents, sans quoi le retryFeedback serait vide.
        var thirdRightAttempt = captured[3];
        Assert.Equal(Direction.Right, thirdRightAttempt.Resolved);
        Assert.Equal(new[] { Direction.Right }, thirdRightAttempt.RejectedKeys);
    }

    // Filet de sécurité : quoi qu'il arrive dans le pipeline, le front doit recevoir un event de fin.
    // Sans lui, une exception inattendue (cf. le crash ValidateContext ci-dessus) laisse le joueur IA
    // suspendu à N/4 indéfiniment, sans AiClueGenerationFailed ni AiPlayerBoardFailed.
    [Fact]
    public async Task Unexpected_exception_mid_board_still_publishes_board_failure_events()
    {
        var fake = new FakeChatClient();
        var calls = 0;
        var sp = BuildPerDirection(fake, promptBuild: _ =>
        {
            calls++;
            if (calls == 2)
                throw new ArgumentException("boom");
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

        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await sp.GetRequiredService<IGenerateAICluesUseCase>()
            .Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(1, response.SucceededCount);
        Assert.Equal(3, response.FailedCount);

        var failed = events.PublishedEvents.OfType<AiClueGenerationFailed>().ToList();
        Assert.Contains(failed, e => e.Direction == Direction.Right);
        Assert.Contains(failed, e => e.Direction == Direction.Bottom);
        Assert.Contains(failed, e => e.Direction == Direction.Left);
        Assert.Contains(events.PublishedEvents.OfType<AiPlayerBoardFailed>(), _ => true);

        var game = await repo.Get(gameId);
        Assert.False(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    private static string PickConflictWord(CloverBoard board)
    {
        // Réutilise le pattern du test PerBoard : prend un mot apparaissant déjà sur le board pour forcer un rejet.
        return board.TopLeft!.GetWord(Direction.Top);
    }
}
