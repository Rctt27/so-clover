using SoClover.Domain;
using SoClover.Tests.Helpers;
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
        game.InitializeWordsPoolAsync(new TestWordDictionary()).GetAwaiter().GetResult();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        Assert.Throws<InvalidOperationInPhaseException>(() => game.SetGuessAiBoardOnly(true));
    }

    [Fact]
    public void RemovePlayer_auto_disables_GuessAiBoardOnly_when_last_AI_is_removed()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot);
        game.SetGuessAiBoardOnly(true);
        Assert.True(game.GuessAiBoardOnly);

        game.RemovePlayer(bot.Id);

        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void RemovePlayer_keeps_GuessAiBoardOnly_when_other_AI_remains()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot1 = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        var bot2 = new Player(PlayerId.New(), "Bot-2", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(human);
        game.AddPlayer(bot1);
        game.AddPlayer(bot2);
        game.SetGuessAiBoardOnly(true);

        game.RemovePlayer(bot1.Id);

        Assert.True(game.GuessAiBoardOnly);
    }

    [Fact]
    public void RemovePlayer_does_not_re_enable_flag_when_human_is_removed()
    {
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(admin);
        game.AddPlayer(bob);
        game.AddPlayer(bot);
        game.SetGuessAiBoardOnly(true);

        game.RemovePlayer(bob.Id);

        Assert.True(game.GuessAiBoardOnly);
    }

}
