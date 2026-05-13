using Microsoft.Extensions.DependencyInjection;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.UseCases;

public class SetClueWithValidationTests
{
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries");
        services.AddSingleton<IWordDictionary>(sp => new FileWordDictionary(Path.GetFullPath(dictionaryPath)));
        services.AddSingleton<IClock>(sp => new TestClock(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<IGameSettingsProvider>(sp => new TestGameSettingsProvider());
        services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();
        services.AddTransient<CardFactory>();
        services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
        services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
        services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
        services.AddTransient<ISetClueUseCase, SetClue.Handler>();
        services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
        return services.BuildServiceProvider();
    }

    private async Task<(ServiceProvider sp, GameId gameId, PlayerId playerId)> StartWritingPhaseAsync()
    {
        var sp = BuildProvider();
        var create = sp.GetRequiredService<ICreateGameUseCase>();
        var join = sp.GetRequiredService<IJoinGameUseCase>();
        var startWriting = sp.GetRequiredService<IStartWritingPhaseUseCase>();

        var createResponse = await create.Handle(new CreateGame.Request("Admin"));
        var gameId = createResponse.GameId;
        var adminId = createResponse.CreatorPlayerId;
        await join.Handle(new JoinGame.Request(gameId, "Alice"));
        await startWriting.Handle(new StartWritingPhase.Request(gameId));
        return (sp, gameId, adminId);
    }

    [Fact]
    public async Task Invalid_clue_returns_IsValid_false()
    {
        var (sp, gameId, playerId) = await StartWritingPhaseAsync();
        var repo = sp.GetRequiredService<IGameRepository>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();

        // Pick a board word to use as the (invalid) clue
        var game = await repo.Get(gameId);
        var player = game!.Players.First(p => p.Id == playerId);
        var boardWord = player.Board.TopLeft!.Card.TopWord;

        var response = await setClue.Handle(
            new SetClue.Request(gameId, playerId, Direction.Right, boardWord));

        Assert.False(response.Validation.IsValid);
        Assert.NotEmpty(response.Validation.Errors);
    }

    [Fact]
    public async Task Invalid_clue_clears_previous_clue_from_board()
    {
        var (sp, gameId, playerId) = await StartWritingPhaseAsync();
        var repo = sp.GetRequiredService<IGameRepository>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();

        // First, save a valid clue
        await setClue.Handle(new SetClue.Request(gameId, playerId, Direction.Top, "zxqjkv"));

        var game = await repo.Get(gameId);
        var player = game!.Players.First(p => p.Id == playerId);
        Assert.NotNull(player.Board.TopClue);

        // Now submit an invalid clue for the same direction.
        // Pick a board word >= 3 chars so the FrenchOffClueValidator (MinWordLength=3) catches it.
        var boardWord = new[] { player.Board.TopLeft, player.Board.TopRight, player.Board.BottomRight, player.Board.BottomLeft }
            .Where(o => o != null)
            .SelectMany(o => new[] { o!.Card.TopWord, o.Card.RightWord, o.Card.BottomWord, o.Card.LeftWord })
            .First(w => w.Length >= 3);
        await setClue.Handle(new SetClue.Request(gameId, playerId, Direction.Top, boardWord));

        game = await repo.Get(gameId);
        player = game!.Players.First(p => p.Id == playerId);
        Assert.Null(player.Board.TopClue);
    }

    [Fact]
    public async Task Valid_clue_returns_IsValid_true_and_persists()
    {
        var (sp, gameId, playerId) = await StartWritingPhaseAsync();
        var repo = sp.GetRequiredService<IGameRepository>();
        var setClue = sp.GetRequiredService<ISetClueUseCase>();

        var response = await setClue.Handle(
            new SetClue.Request(gameId, playerId, Direction.Top, "zxqjkv"));

        Assert.True(response.Validation.IsValid);
        Assert.Empty(response.Validation.Errors);

        var game = await repo.Get(gameId);
        var player = game!.Players.First(p => p.Id == playerId);
        Assert.NotNull(player.Board.TopClue);
        Assert.Equal("zxqjkv", player.Board.TopClue!.Value.Value);
    }
}
