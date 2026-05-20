using SoClover.Domain;
using Xunit;

namespace SoClover.Tests.UseCases;

public class GuessAiBoardOnlyDomainTests
{
    [Fact]
    public void GuessAiBoardOnly_defaults_to_false_on_new_game()
    {
        var game = new Game(GameId.New());
        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void WritingParticipants_equals_ActivePlayers_when_flag_is_false()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);

        Assert.Equal(game.ActivePlayers.Count, game.WritingParticipants.Count);
        Assert.Contains(game.WritingParticipants, p => p.Id == human.Id);
        Assert.Contains(game.WritingParticipants, p => p.Id == bot.Id);
    }

    [Fact]
    public void WritingParticipants_excludes_humans_when_flag_is_true()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);

        game.SetGuessAiBoardOnly(true);

        Assert.True(game.GuessAiBoardOnly);
        Assert.Single(game.WritingParticipants);
        Assert.Contains(game.WritingParticipants, p => p.Id == bot.Id);
        Assert.DoesNotContain(game.WritingParticipants, p => p.Id == human.Id);
    }

    [Fact]
    public void WritingParticipants_excludes_disconnected_AI_when_flag_is_true()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);
        game.SetGuessAiBoardOnly(true);

        bot.MarkDisconnected();

        Assert.Empty(game.WritingParticipants);
    }

    [Fact]
    public void SetGuessAiBoardOnly_true_throws_NoAiPlayerForGuessAiBoardOnlyException_when_no_AI_present()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(human);

        Assert.Throws<NoAiPlayerForGuessAiBoardOnlyException>(
            () => game.SetGuessAiBoardOnly(true));
        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void SetGuessAiBoardOnly_true_succeeds_when_at_least_one_AI_present()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);

        game.SetGuessAiBoardOnly(true);

        Assert.True(game.GuessAiBoardOnly);
    }

    [Fact]
    public void SetGuessAiBoardOnly_false_is_always_allowed_even_without_AI()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(human);

        game.SetGuessAiBoardOnly(false);

        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void SetGuessAiBoardOnly_throws_InvalidOperationInPhaseException_outside_Lobby()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);
        game.InitializeWordsPoolAsync(new InMemoryDictionary()).GetAwaiter().GetResult();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        Assert.Throws<InvalidOperationInPhaseException>(() => game.SetGuessAiBoardOnly(true));
    }

    private sealed class InMemoryDictionary : IWordDictionary
    {
        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)Enumerable.Range(0, count).Select(i => $"Word{i}").ToList());
        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)new List<string> { "Word1", "Word2" });
    }
}
