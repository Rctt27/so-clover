using SoClover.Domain;

namespace SoClover.Tests.Helpers;

internal static class GuessingPhaseGameBuilder
{
    internal static (Game Game, PlayerId OwnerId, PlayerId GuesserId) CreateGameInGuessingPhase()
    {
        var game = new Game(GameId.New());
        var ownerId = PlayerId.New();
        var guesserId = PlayerId.New();

        game.AddPlayer(new Player(ownerId, "Owner"));
        game.AddPlayer(new Player(guesserId, "Guesser"));

        game.InitializeWordsPoolAsync(new TestWordDictionary()).Wait();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        var owner = game.Players.First(p => p.Id == ownerId);
        owner.Board.Place(BoardPosition.TopLeft, new OrientedCard(new Card(CardId.New(), "A1", "A2", "A3", "A4")));
        owner.Board.Place(BoardPosition.TopRight, new OrientedCard(new Card(CardId.New(), "B1", "B2", "B3", "B4")));
        owner.Board.Place(BoardPosition.BottomRight, new OrientedCard(new Card(CardId.New(), "C1", "C2", "C3", "C4")));
        owner.Board.Place(BoardPosition.BottomLeft, new OrientedCard(new Card(CardId.New(), "D1", "D2", "D3", "D4")));

        var fifthCard = new Card(CardId.New(), "W1", "W2", "W3", "W4");
        var rotations = new[] { Rotation.None, Rotation.None, Rotation.None, Rotation.None, Rotation.None };

        game.StartGuessingPhase(ownerId, fifthCard, rotations, DateTime.UtcNow, TimeSpan.FromMinutes(5));
        return (game, ownerId, guesserId);
    }
}
