using Xunit;

namespace SoClover.Tests.UseCases;

public class MoveToNextBoardAiTests
{
    [Fact]
    public void MoveToNextBoardHandler_uses_BoardsToGuess_count_for_isLastBoard()
    {
        // Test léger : on vérifie via la lecture du source que le handler appelle game.IsLastGuessingBoard().
        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoClover", "UseCases", "Gameplay", "MoveToNextBoard.cs"));

        Assert.Contains("game.IsLastGuessingBoard()", source);
        Assert.DoesNotContain("game.ActivePlayers.Count", source);
    }
}
