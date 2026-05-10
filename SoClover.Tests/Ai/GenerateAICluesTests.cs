using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
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
}
