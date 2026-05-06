using SoClover.Domain;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.Abstractions;
using Xunit;
using Moq;

namespace SoClover.Tests;

public class RotateBoardTests
{
    private class DummyDictionary : IWordDictionary
    {
        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<string>)Enumerable.Range(0, count).Select(i => $"Word{i}").ToList());
        }
        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<string>)new List<string> { "Word1", "Word2" });
        }
    }

    private Game CreateGameInGuessingPhase(out PlayerId ownerId, out PlayerId guesserId)
    {
        var game = new Game(GameId.New());
        ownerId = PlayerId.New();
        guesserId = PlayerId.New();
        
        game.AddPlayer(new Player(ownerId, "Owner"));
        game.AddPlayer(new Player(guesserId, "Guesser"));
        
        game.InitializeWordsPoolAsync(new DummyDictionary()).Wait();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        
        var localOwnerId = ownerId;
        var owner = game.Players.First(p => p.Id == localOwnerId);
        owner.Board.Place(BoardPosition.TopLeft,     new OrientedCard(new Card(CardId.New(), "A1", "A2", "A3", "A4")));
        owner.Board.Place(BoardPosition.TopRight,    new OrientedCard(new Card(CardId.New(), "B1", "B2", "B3", "B4")));
        owner.Board.Place(BoardPosition.BottomRight, new OrientedCard(new Card(CardId.New(), "C1", "C2", "C3", "C4")));
        owner.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(new Card(CardId.New(), "D1", "D2", "D3", "D4")));

        var fifthCard = new Card(CardId.New(), "W1", "W2", "W3", "W4");
        var rotations = new[] { Rotation.None, Rotation.None, Rotation.None, Rotation.None, Rotation.None };
        
        game.StartGuessingPhase(ownerId, fifthCard, rotations, DateTime.UtcNow, TimeSpan.FromMinutes(5));
        return game;
    }

    [Fact]
    public async Task Handle_ShouldRotateBoard()
    {
        // Arrange
        var game = CreateGameInGuessingPhase(out var ownerId, out var guesserId);
        var repoMock = new Mock<IGameRepository>();
        repoMock.Setup(r => r.Get(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var eventsMock = new Mock<IEventPublisher>();
        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);
        
        var handler = new RotateBoard.Handler(repoMock.Object, eventsMock.Object, clockMock.Object);

        // Act
        await handler.Handle(new RotateBoard.Request(game.Id, guesserId, 90));

        // Assert
        Assert.Equal(90, game.CumulativeBoardRotation);
        repoMock.Verify(r => r.Save(game, It.IsAny<CancellationToken>()), Times.Once);
        eventsMock.Verify(e => e.Publish(It.Is<BoardRotated>(ev => ev.Revision == game.Revision && ev.Revision > 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowIfOwnerTriesToRotateBoard()
    {
        // Arrange
        var game = CreateGameInGuessingPhase(out var ownerId, out var guesserId);
        var repoMock = new Mock<IGameRepository>();
        repoMock.Setup(r => r.Get(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var eventsMock = new Mock<IEventPublisher>();
        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);
        
        var handler = new RotateBoard.Handler(repoMock.Object, eventsMock.Object, clockMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            handler.Handle(new RotateBoard.Request(game.Id, ownerId, 90)));
    }
}
