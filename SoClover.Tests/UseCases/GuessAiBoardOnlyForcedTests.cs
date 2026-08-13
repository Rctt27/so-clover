using SoClover.Domain;
using SoClover.Tests.Helpers;
using Xunit;

namespace SoClover.Tests.UseCases;

/// <summary>
/// Un humain seul face à des IA ne peut pas voir son propre plateau deviné (les IA ne devinent pas).
/// Dans cette configuration le mode « deviner uniquement les plateaux IA » devient obligatoire :
/// il s'active tout seul et ne peut plus être décoché tant qu'un second humain n'a pas rejoint.
/// </summary>
public class GuessAiBoardOnlyForcedTests
{
    private static Player Human(string name, bool isAdmin = false)
        => new(PlayerId.New(), name, isAdmin);

    private static Player Bot(string name)
        => new(PlayerId.New(), name, isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

    [Fact]
    public void Forced_is_false_when_no_AI_player()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.AddPlayer(Human("Bob"));

        Assert.False(game.GuessAiBoardOnlyForced);
        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void Forced_is_false_when_a_single_human_has_no_AI_companion()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));

        Assert.False(game.GuessAiBoardOnlyForced);
    }

    [Fact]
    public void Forced_is_false_when_two_humans_and_one_AI()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.AddPlayer(Human("Bob"));
        game.AddAIPlayer(Bot("Bot-1"), max: 3);

        Assert.False(game.GuessAiBoardOnlyForced);
        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void Forced_is_false_when_the_only_AI_is_disconnected()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        var bot = Bot("Bot-1");
        game.AddAIPlayer(bot, max: 3);

        bot.MarkDisconnected();

        Assert.False(game.GuessAiBoardOnlyForced);
    }

    [Fact]
    public void AddAIPlayer_auto_enables_GuessAiBoardOnly_when_a_single_human_is_present()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));

        game.AddAIPlayer(Bot("Bot-1"), max: 3);

        Assert.True(game.GuessAiBoardOnlyForced);
        Assert.True(game.GuessAiBoardOnly);
    }

    [Fact]
    public void SetGuessAiBoardOnly_false_throws_when_forced()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.AddAIPlayer(Bot("Bot-1"), max: 3);

        Assert.Throws<GuessAiBoardOnlyRequiredException>(() => game.SetGuessAiBoardOnly(false));
        Assert.True(game.GuessAiBoardOnly);
    }

    [Fact]
    public void SetGuessAiBoardOnly_false_succeeds_once_a_second_human_joined()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.AddAIPlayer(Bot("Bot-1"), max: 3);
        Assert.True(game.GuessAiBoardOnly);

        game.AddPlayer(Human("Bob"));

        Assert.False(game.GuessAiBoardOnlyForced);
        Assert.True(game.GuessAiBoardOnly); // le réglage reste actif, mais devient modifiable
        game.SetGuessAiBoardOnly(false);
        Assert.False(game.GuessAiBoardOnly);
    }

    [Fact]
    public void RemovePlayer_auto_enables_GuessAiBoardOnly_when_only_one_human_remains()
    {
        var game = new Game(GameId.New());
        var alice = Human("Alice", isAdmin: true);
        var bob = Human("Bob");
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        game.AddAIPlayer(Bot("Bot-1"), max: 3);
        Assert.False(game.GuessAiBoardOnly);

        game.RemovePlayer(bob.Id);

        Assert.True(game.GuessAiBoardOnlyForced);
        Assert.True(game.GuessAiBoardOnly);
    }

    [Fact]
    public void Forced_is_false_outside_the_Lobby()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.AddAIPlayer(Bot("Bot-1"), max: 3);
        game.InitializeWordsPoolAsync(new TestWordDictionary()).GetAwaiter().GetResult();

        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        Assert.False(game.GuessAiBoardOnlyForced);
        Assert.True(game.GuessAiBoardOnly); // le réglage gelé au démarrage reste actif
    }

    [Fact]
    public void StartWritingPhase_throws_when_a_human_is_alone_in_the_lobby()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        game.InitializeWordsPoolAsync(new TestWordDictionary()).GetAwaiter().GetResult();

        var ex = Assert.Throws<NotEnoughPlayersException>(
            () => game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5)));
        Assert.Equal(2, ex.RequiredMinimum);
    }

    [Fact]
    public void StartWritingPhase_throws_when_forced_mode_is_not_active()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(Human("Alice", isAdmin: true));
        var bob = Human("Bob");
        game.AddPlayer(bob);
        game.AddAIPlayer(Bot("Bot-1"), max: 3);
        game.InitializeWordsPoolAsync(new TestWordDictionary()).GetAwaiter().GetResult();
        // Bob se déconnecte sans quitter le lobby : Alice devient le seul humain actif,
        // mais le réglage n'a pas été resynchronisé par AddPlayer/RemovePlayer.
        bob.MarkDisconnected();

        Assert.Throws<GuessAiBoardOnlyRequiredException>(
            () => game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5)));
    }
}
