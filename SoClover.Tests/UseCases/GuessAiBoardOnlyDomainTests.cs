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
}
