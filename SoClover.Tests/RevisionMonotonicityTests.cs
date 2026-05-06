using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class RevisionMonotonicityTests
{
    [Fact]
    public void NewGameStartsAtRevisionZero()
    {
        var game = new Game(GameId.New());
        Assert.Equal(0, game.Revision);
    }

    [Fact]
    public void AddPlayerBumpsRevision()
    {
        var game = new Game(GameId.New());
        var before = game.Revision;
        game.AddPlayer(new Player(PlayerId.New(), "Alice"));
        Assert.Equal(before + 1, game.Revision);
    }

    [Fact]
    public void RotateBoardBumpsRevision()
    {
        var (game, _, _) = RevisionTestSetup.CreateInGuessingPhase();
        var before = game.Revision;
        game.RotateBoard(90);
        Assert.Equal(before + 1, game.Revision);
    }

    [Fact]
    public void RevisionIsStrictlyIncreasingAcrossMixedMutations()
    {
        var game = new Game(GameId.New());
        var revisions = new List<int> { game.Revision };

        game.AddPlayer(new Player(PlayerId.New(), "Alice"));
        revisions.Add(game.Revision);
        game.AddPlayer(new Player(PlayerId.New(), "Bob"));
        revisions.Add(game.Revision);

        for (int i = 1; i < revisions.Count; i++)
            Assert.True(revisions[i] > revisions[i - 1],
                $"Revision regressed at step {i}: {revisions[i - 1]} -> {revisions[i]}");
    }
}

internal static class RevisionTestSetup
{
    public static (Game game, PlayerId owner, PlayerId guesser) CreateInGuessingPhase()
    {
        var game = new Game(GameId.New());
        var owner = PlayerId.New();
        var guesser = PlayerId.New();

        game.AddPlayer(new Player(owner, "Owner"));
        game.AddPlayer(new Player(guesser, "Guesser"));
        game.InitializeWordsPoolAsync(new RevisionDummyDictionary()).Wait();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        var localOwner = owner;
        var ownerPlayer = game.Players.First(p => p.Id == localOwner);
        ownerPlayer.Board.Place(BoardPosition.TopLeft,     new OrientedCard(new Card(CardId.New(), "A1", "A2", "A3", "A4")));
        ownerPlayer.Board.Place(BoardPosition.TopRight,    new OrientedCard(new Card(CardId.New(), "B1", "B2", "B3", "B4")));
        ownerPlayer.Board.Place(BoardPosition.BottomRight, new OrientedCard(new Card(CardId.New(), "C1", "C2", "C3", "C4")));
        ownerPlayer.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(new Card(CardId.New(), "D1", "D2", "D3", "D4")));

        var fifthCard = new Card(CardId.New(), "W1", "W2", "W3", "W4");
        var rotations = new[] { Rotation.None, Rotation.None, Rotation.None, Rotation.None, Rotation.None };

        game.StartGuessingPhase(owner, fifthCard, rotations, DateTime.UtcNow, TimeSpan.FromMinutes(5));
        return (game, owner, guesser);
    }

    private sealed class RevisionDummyDictionary : IWordDictionary
    {
        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)Enumerable.Range(0, count).Select(i => $"Word{i}").ToList());

        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)new List<string> { "Word1", "Word2" });
    }
}
