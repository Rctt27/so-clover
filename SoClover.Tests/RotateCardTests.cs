using SoClover.Domain;
using SoClover.Tests.Helpers;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.Abstractions;
using Xunit;
using Moq;

namespace SoClover.Tests;

public class RotateCardTests
{
    [Fact]
    public async Task Handle_ShouldRotateOutsideCard()
    {
        // Arrange
        var (game, _, guesserId) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        var repoMock = new Mock<IGameRepository>();
        repoMock.Setup(r => r.Get(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var eventsMock = new Mock<IEventPublisher>();
        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        var handler = new RotateCard.Handler(repoMock.Object, eventsMock.Object, clockMock.Object);
        var initialRotation = game.OutsideCards[0]!.Rotation;

        // Act
        await handler.Handle(new RotateCard.Request(game.Id, guesserId, 0, null, 1));

        // Assert
        Assert.Equal((Rotation)(((int)initialRotation + 1) % 4), game.OutsideCards[0]!.Rotation);
        repoMock.Verify(r => r.Save(game, It.IsAny<CancellationToken>()), Times.Once);
        eventsMock.Verify(e => e.Publish(It.IsAny<CardRotated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRotateBoardCard()
    {
        // Arrange
        var (game, _, guesserId) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        // Placer une carte sur le board d'abord
        game.PlaceCardOnGuessingBoard(0, BoardPosition.TopLeft);

        var repoMock = new Mock<IGameRepository>();
        repoMock.Setup(r => r.Get(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var eventsMock = new Mock<IEventPublisher>();
        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        var handler = new RotateCard.Handler(repoMock.Object, eventsMock.Object, clockMock.Object);
        var initialRotation = game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation;

        // Act
        await handler.Handle(new RotateCard.Request(game.Id, guesserId, null, BoardPosition.TopLeft, 1));

        // Assert
        Assert.Equal((Rotation)(((int)initialRotation + 1) % 4), game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);
        repoMock.Verify(r => r.Save(game, It.IsAny<CancellationToken>()), Times.Once);
        eventsMock.Verify(e => e.Publish(It.IsAny<CardRotated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowIfOwnerTriesToRotate()
    {
        // Arrange
        var (game, ownerId, _) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();
        var repoMock = new Mock<IGameRepository>();
        repoMock.Setup(r => r.Get(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var eventsMock = new Mock<IEventPublisher>();
        var clockMock = new Mock<IClock>();
        clockMock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

        var handler = new RotateCard.Handler(repoMock.Object, eventsMock.Object, clockMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RotateCard.Request(game.Id, ownerId, 0, null, 1)));
    }
}
