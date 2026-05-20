using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SoClover.Tests;

public class DisconnectPlayerTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp =>
            new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
        services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
        services.AddTransient<IDisconnectPlayerUseCase, DisconnectPlayer.Handler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Disconnect_during_writing_marks_player_and_does_not_block_guessing()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();
        var submitBoard = sp.GetRequiredService<ISubmitBoardUseCase>();
        var disconnect = sp.GetRequiredService<IDisconnectPlayerUseCase>();
        var repo = sp.GetRequiredService<IGameRepository>();

        var gameResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = gameResponse.GameId;
        var adminId = gameResponse.CreatorPlayerId;
        var aliceId = (await join.Handle(new JoinGame.Request(gameId, "Alice"))).PlayerId;

        await startWriting.Handle(new StartWritingPhase.Request(gameId));

        // Alice disconnects during WritingClues
        var result = await disconnect.Handle(new DisconnectPlayer.Request(gameId, aliceId));
        Assert.True(result.Success);

        var game = await repo.Get(gameId);
        var alice = game!.Players.First(p => p.Id == aliceId);
        Assert.True(alice.IsDisconnected);
        Assert.Single(game.ActivePlayers);

        // Admin submits clues — game should auto-transition since only active player submitted
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Top, "c1"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Right, "c2"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Bottom, "c3"));
        await setClue.Handle(new SetClue.Request(gameId, adminId, Direction.Left, "c4"));
        await submitBoard.Handle(new SubmitBoard.Request(gameId, adminId));

        game = await repo.Get(gameId);
        Assert.Equal(GamePhase.Guessing, game!.Phase);

        // Verify disconnected player has a BoardResult
        Assert.True(game.BoardResults.ContainsKey(aliceId));
        Assert.False(game.BoardResults[aliceId].WasGuessed);
        Assert.True(game.BoardResults[aliceId].IsDisconnected);
    }

    [Fact]
    public async Task DisconnectPlayer_in_GuessAiBoardOnly_does_not_use_ActivePlayers_for_all_submitted_check()
    {
        var sp = BuildProvider();
        var repo = sp.GetRequiredService<IGameRepository>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();
        var disconnect = sp.GetRequiredService<IDisconnectPlayerUseCase>();

        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        game.AddAIPlayer(bot, max: 4);
        game.SetGuessAiBoardOnly(true);
        await repo.Save(game);

        await startWriting.Handle(new StartWritingPhase.Request(game.Id));

        var withAi = (await repo.Get(game.Id))!;
        SoClover.Tests.Helpers.AiTestHelpers.SimulateAiBoardSubmit(withAi, bot.Id, DateTime.UtcNow);
        await repo.Save(withAi);

        // Bob se déconnecte → WritingParticipants = [bot], tous submitted → Guessing.
        await disconnect.Handle(new DisconnectPlayer.Request(game.Id, bob.Id));

        var finalState = (await repo.Get(game.Id))!;
        Assert.Equal(GamePhase.Guessing, finalState.Phase);
    }
}
