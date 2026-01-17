using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class DomainRotationTests
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

    private Game CreateGameInGuessingPhase(out PlayerId ownerId, out Card fifthCard)
    {
        var game = new Game(GameId.New());
        ownerId = PlayerId.New();
        var player = new Player(ownerId, "Owner");
        game.AddPlayer(player);
        
        game.InitializeWordsPoolAsync(new DummyDictionary()).Wait();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        
        // Peupler le board du joueur pour éviter NullReferenceException dans StartGuessingPhase
        player.Board.Place(BoardPosition.TopLeft,     new OrientedCard(new Card(CardId.New(), "A1", "A2", "A3", "A4")));
        player.Board.Place(BoardPosition.TopRight,    new OrientedCard(new Card(CardId.New(), "B1", "B2", "B3", "B4")));
        player.Board.Place(BoardPosition.BottomRight, new OrientedCard(new Card(CardId.New(), "C1", "C2", "C3", "C4")));
        player.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(new Card(CardId.New(), "D1", "D2", "D3", "D4")));

        fifthCard = new Card(CardId.New(), "W1", "W2", "W3", "W4");
        var rotations = new[] { Rotation.None, Rotation.None, Rotation.None, Rotation.None, Rotation.None };
        
        game.StartGuessingPhase(ownerId, fifthCard, rotations, DateTime.UtcNow, TimeSpan.FromMinutes(5));
        return game;
    }

    [Fact]
    public void PlaceCardOnGuessingBoard_ShouldCompensateBoardRotation()
    {
        // Arrange
        var game = CreateGameInGuessingPhase(out _, out _);
        
        // Faire pivoter le plateau de 90°
        game.RotateBoard(90);
        
        // Act
        // Placer la 5ème carte (index 4) du pool sur TopLeft
        game.PlaceCardOnGuessingBoard(4, BoardPosition.TopLeft);
        
        // Assert
        var placedCard = game.GuessedCardPositions[BoardPosition.TopLeft];
        Assert.NotNull(placedCard);
        // Rotation attendue : None (0) - 90/90 (1) = -1 => 3 (Right270)
        Assert.Equal(Rotation.Right270, placedCard.Rotation);
    }

    [Fact]
    public void ReturnGuessingCard_ShouldInvertCompensation()
    {
        // Arrange
        var game = CreateGameInGuessingPhase(out _, out _);
        
        game.RotateBoard(180); // 2 steps
        
        // Place card: None (0) - 2 steps = 2 (Right180)
        game.PlaceCardOnGuessingBoard(4, BoardPosition.TopLeft);
        Assert.Equal(Rotation.Right180, game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);
        
        // Act
        game.ReturnGuessingCard(BoardPosition.TopLeft);
        
        // Assert
        // La carte doit être revenue dans le pool à l'index 4 (puisque c'était le premier slot vide)
        var returnedCard = game.OutsideCards[4];
        Assert.NotNull(returnedCard);
        // Rotation attendue : Right180 (2) + 180/90 (2) = 4 => 0 (None)
        Assert.Equal(Rotation.None, returnedCard.Rotation);
    }

    [Fact]
    public void PlaceCard_WhenOverwriting_ShouldCorrectlyHandleBothCards()
    {
         // Arrange
        var game = CreateGameInGuessingPhase(out _, out var card2);
        var card1 = new Card(CardId.New(), "C1-1", "C1-2", "C1-3", "C1-4");
        // Remplacer manuellement card à l'index 0 pour le test
        game.OutsideCards[0] = new OrientedCard(card1, Rotation.None);
        
        game.RotateBoard(270); // 3 steps
        
        // Placer card2 (index 4) sur TopLeft
        game.PlaceCardOnGuessingBoard(4, BoardPosition.TopLeft);
        Assert.Equal(Rotation.Right90, game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);
        
        // On veut maintenant placer card1 (index 0) sur TopLeft (occupé par card2)
        // Act
        game.PlaceCardOnGuessingBoard(0, BoardPosition.TopLeft);
        
        // Assert
        // card1 sur board doit avoir rotation 0 - 3 = 1 (Right90)
        Assert.Equal(Rotation.Right90, game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);
        
        // card2 renvoyée au pool (index 0) doit avoir sa rotation restaurée : 1 + 3 = 4 => 0 (None)
        Assert.Equal(Rotation.None, game.OutsideCards[0]!.Rotation);
    }

    [Fact]
    public void SwapGuessingCards_ShouldMaintainVisualRotation()
    {
        // Arrange
        var game = CreateGameInGuessingPhase(out _, out _);
        game.RotateBoard(90); // 1 step
        
        // Placer une carte (index 4) sur TopLeft -> rotation sera compensée : None (0) - 1 = Right270 (3)
        game.PlaceCardOnGuessingBoard(4, BoardPosition.TopLeft);
        Assert.Equal(Rotation.Right270, game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);

        // Placer une autre carte (index 0) sur TopRight -> rotation sera compensée : None (0) - 1 = Right270 (3)
        game.PlaceCardOnGuessingBoard(0, BoardPosition.TopRight);
        Assert.Equal(Rotation.Right270, game.GuessedCardPositions[BoardPosition.TopRight]!.Rotation);

        // Act
        game.SwapGuessingCards(BoardPosition.TopLeft, BoardPosition.TopRight);

        // Assert
        // Les rotations ne doivent PAS avoir changé (elles restent Right270)
        Assert.Equal(Rotation.Right270, game.GuessedCardPositions[BoardPosition.TopLeft]!.Rotation);
        Assert.Equal(Rotation.Right270, game.GuessedCardPositions[BoardPosition.TopRight]!.Rotation);
    }
}
