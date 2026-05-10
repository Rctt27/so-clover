using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAICluesTests
{
    [Fact]
    public async Task HappyPath_one_call_four_valid_clues_emits_4_AiClueGenerated_and_auto_submits()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    "soleil", "Mer + plage"),
            (Direction.Right,  "orage",  "Pluie + foudre"),
            (Direction.Bottom, "noir",   "Nuit + ombre"),
            (Direction.Left,   "feu",    "Chaud + flamme"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(1, response.LlmCallsConsumed);

        var generated = events.PublishedEvents.OfType<AiClueGenerated>().ToList();
        Assert.Equal(4, generated.Count);
        Assert.Contains(generated, e => e.Direction == Direction.Top    && e.ClueText == "soleil");
        Assert.Contains(generated, e => e.Direction == Direction.Right  && e.ClueText == "orage");
        Assert.Contains(generated, e => e.Direction == Direction.Bottom && e.ClueText == "noir");
        Assert.Contains(generated, e => e.Direction == Direction.Left   && e.ClueText == "feu");

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        Assert.True(game!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task RetryPartial_first_call_2_valid_second_call_2_remaining_emits_4_events_auto_submits()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        var board = game!.Players.First(p => p.Id == aiPid).Board;
        var conflict = PickConflictWord(board);

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    "soleil",   "ok"),
            (Direction.Right,  conflict,   "conflit"),
            (Direction.Bottom, "noir",     "ok"),
            (Direction.Left,   conflict,   "conflit"),
        });
        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Right, "orage", "Pluie + foudre"),
            (Direction.Left,  "feu",   "Chaud + flamme"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(2, response.LlmCallsConsumed);
        Assert.Equal(4, events.PublishedEvents.OfType<AiClueGenerated>().Count());

        var refreshed = await repo.Get(gameId);
        Assert.True(refreshed!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task RetryPartial_second_call_only_requests_remaining_directions_with_rejection_feedback()
    {
        BoardCluesPromptContext? secondCallContext = null;
        var callCount = 0;
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake, promptBuild: ctx =>
        {
            callCount++;
            if (callCount == 2) secondCallContext = ctx;
            return new AiCluePromptBundle("S", "U", "{}");
        });
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        var board = game!.Players.First(p => p.Id == aiPid).Board;
        var conflict = PickConflictWord(board);

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    "soleil",  "ok"),
            (Direction.Right,  conflict,  "conflit"),
            (Direction.Bottom, "noir",    "ok"),
            (Direction.Left,   "feu",     "ok"),
        });
        AiTestProvider.EnqueueValidJson(fake, new[] { (Direction.Right, "orage", "ok") });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.NotNull(secondCallContext);
        Assert.Equal(new[] { Direction.Right }, secondCallContext!.Value.RemainingDirections);
        Assert.True(secondCallContext.Value.RejectedPerDirection.ContainsKey(Direction.Right));
        Assert.Single(secondCallContext.Value.RejectedPerDirection[Direction.Right]);
        Assert.Equal(conflict, secondCallContext.Value.RejectedPerDirection[Direction.Right][0].ClueText);
    }

    [Fact]
    public async Task Idempotent_when_board_already_has_4_clues_short_circuits_with_zero_LLM_calls()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var validatorFactory = sp.GetRequiredService<IClueValidatorFactory>();
        var game = await repo.Get(gameId);
        var validator = validatorFactory.GetFor(game!.Language, game.SemanticClueCheckEnabled);
        game.SetClue(aiPid, Direction.Top,    "soleil", validator);
        game.SetClue(aiPid, Direction.Right,  "orage",  validator);
        game.SetClue(aiPid, Direction.Bottom, "noir",   validator);
        game.SetClue(aiPid, Direction.Left,   "feu",    validator);
        await repo.Save(game);

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(0, response.LlmCallsConsumed);
        Assert.Equal(0, fake.CallCount);
        Assert.Empty(events.PublishedEvents.OfType<AiClueGenerated>());
        Assert.Empty(events.PublishedEvents.OfType<AiClueGenerationFailed>());
    }

    [Fact]
    public async Task Idempotent_when_2_clues_already_set_only_requests_remaining_2()
    {
        BoardCluesPromptContext? firstCallContext = null;
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake, promptBuild: ctx =>
        {
            firstCallContext ??= ctx;
            return new AiCluePromptBundle("S", "U", "{}");
        });
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var validatorFactory = sp.GetRequiredService<IClueValidatorFactory>();
        var game = await repo.Get(gameId);
        var validator = validatorFactory.GetFor(game!.Language, game.SemanticClueCheckEnabled);
        game.SetClue(aiPid, Direction.Top,   "soleil", validator);
        game.SetClue(aiPid, Direction.Right, "orage",  validator);
        await repo.Save(game);

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Bottom, "noir", "ok"),
            (Direction.Left,   "feu",  "ok"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(4, response.SucceededCount);
        Assert.Equal(0, response.FailedCount);
        Assert.Equal(1, response.LlmCallsConsumed);

        Assert.NotNull(firstCallContext);
        Assert.Equal(2, firstCallContext!.Value.RemainingDirections.Count);
        Assert.Contains(Direction.Bottom, firstCallContext.Value.RemainingDirections);
        Assert.Contains(Direction.Left, firstCallContext.Value.RemainingDirections);
        Assert.DoesNotContain(Direction.Top, firstCallContext.Value.RemainingDirections);
        Assert.DoesNotContain(Direction.Right, firstCallContext.Value.RemainingDirections);
    }

    [Fact]
    public async Task ExhaustionTotal_3_invalid_responses_emits_4_AiClueGenerationFailed_no_submit()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        var board = game!.Players.First(p => p.Id == aiPid).Board;
        var conflict = PickConflictWord(board);

        for (var i = 0; i < 3; i++)
        {
            AiTestProvider.EnqueueValidJson(fake, new[]
            {
                (Direction.Top,    conflict, "boom"),
                (Direction.Right,  conflict, "boom"),
                (Direction.Bottom, conflict, "boom"),
                (Direction.Left,   conflict, "boom"),
            });
        }

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(0, response.SucceededCount);
        Assert.Equal(4, response.FailedCount);
        Assert.Equal(3, response.LlmCallsConsumed);

        var failures = events.PublishedEvents.OfType<AiClueGenerationFailed>().ToList();
        Assert.Equal(4, failures.Count);
        Assert.All(failures, f => Assert.Equal(3, f.AttemptedClues.Count));

        var refreshed = await repo.Get(gameId);
        Assert.False(refreshed!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task ExhaustionPartial_2_resolved_2_failed_emits_2_Generated_2_Failed_no_submit()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        var board = game!.Players.First(p => p.Id == aiPid).Board;
        var conflict = PickConflictWord(board);

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    "soleil",   "ok"),
            (Direction.Right,  conflict,   "boom"),
            (Direction.Bottom, "noir",     "ok"),
            (Direction.Left,   conflict,   "boom"),
        });
        for (var i = 0; i < 2; i++)
        {
            AiTestProvider.EnqueueValidJson(fake, new[]
            {
                (Direction.Right, conflict, "boom"),
                (Direction.Left,  conflict, "boom"),
            });
        }

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        var response = await useCase.Handle(new GenerateAIClues.Request(gameId, aiPid));

        Assert.Equal(2, response.SucceededCount);
        Assert.Equal(2, response.FailedCount);
        Assert.Equal(3, response.LlmCallsConsumed);

        Assert.Equal(2, events.PublishedEvents.OfType<AiClueGenerated>().Count());
        Assert.Equal(2, events.PublishedEvents.OfType<AiClueGenerationFailed>().Count());

        var refreshed = await repo.Get(gameId);
        Assert.False(refreshed!.Players.First(p => p.Id == aiPid).Board.IsSubmitted);
    }

    [Fact]
    public async Task Budget_exhausted_on_first_attempt_throws_without_calling_LLM()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake, budgetMaxCallsPerGame: 1);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var budget = sp.GetRequiredService<GameLlmBudget>();
        budget.TryConsume(gameId);

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();

        await Assert.ThrowsAsync<LlmBudgetExhaustedException>(() =>
            useCase.Handle(new GenerateAIClues.Request(gameId, aiPid)));

        Assert.Equal(0, fake.CallCount);
    }

    [Fact]
    public async Task Budget_exhausted_between_attempts_persists_already_resolved_directions()
    {
        var fake = new FakeChatClient();
        var sp = AiTestProvider.Build(fake, budgetMaxCallsPerGame: 1);
        var (gameId, aiPids) = await AiTestProvider.SetupGameWithAis(sp);
        var aiPid = aiPids[0];

        var repo = sp.GetRequiredService<IGameRepository>();
        var game = await repo.Get(gameId);
        var board = game!.Players.First(p => p.Id == aiPid).Board;
        var conflict = PickConflictWord(board);

        AiTestProvider.EnqueueValidJson(fake, new[]
        {
            (Direction.Top,    "soleil", "ok"),
            (Direction.Right,  conflict, "boom"),
            (Direction.Bottom, "noir",   "ok"),
            (Direction.Left,   "feu",    "ok"),
        });

        var useCase = sp.GetRequiredService<IGenerateAICluesUseCase>();
        var events = sp.GetRequiredService<InMemoryEventPublisher>();

        await Assert.ThrowsAsync<LlmBudgetExhaustedException>(() =>
            useCase.Handle(new GenerateAIClues.Request(gameId, aiPid)));

        Assert.Equal(3, events.PublishedEvents.OfType<AiClueGenerated>().Count());
        var refreshed = await repo.Get(gameId);
        var b = refreshed!.Players.First(p => p.Id == aiPid).Board;
        Assert.NotNull(b.TopClue);
        Assert.NotNull(b.BottomClue);
        Assert.NotNull(b.LeftClue);
        Assert.Null(b.RightClue);
        Assert.False(b.IsSubmitted);
    }

    /// <summary>
    /// Pick any board card word that is >= 3 chars and <= 32 chars so that
    /// (a) FrenchOffClueValidator actually evaluates it (skips < 3 chars), and
    /// (b) ClueText.Create accepts it as a clue (rejects > 32 chars).
    /// Submitting it as a clue triggers an ExactMatch rejection.
    /// </summary>
    private static string PickConflictWord(CloverBoard board)
    {
        var oriented = new[] { board.TopLeft, board.TopRight, board.BottomRight, board.BottomLeft };
        foreach (var oc in oriented)
        {
            if (oc is null) continue;
            foreach (var w in new[] { oc.Card.TopWord, oc.Card.RightWord, oc.Card.BottomWord, oc.Card.LeftWord })
            {
                if (w.Length >= 3 && w.Length <= 32) return w;
            }
        }
        throw new InvalidOperationException("No board word in [3..32] range — dictionary anomaly?");
    }
}
