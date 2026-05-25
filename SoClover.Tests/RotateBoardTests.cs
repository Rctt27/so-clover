using SoClover.Domain;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.Abstractions;
using Xunit;
using Moq;

namespace SoClover.Tests;

public class RotateBoardTests
{
    [Fact]
    public async Task Handle_ShouldRotateBoard()
    {
        // Arrange
        var (game, _, guesserId) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
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
        var (game, ownerId, _) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
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
