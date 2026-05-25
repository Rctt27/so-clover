using SoClover.Domain;

namespace SoClover.Tests;

internal static class GameTestExtensions
{
    internal static void SubmitAllBoards(this Game game, TestClock clock) =>
        game.ActivePlayers.ToList().ForEach(pl => pl.Board.MarkSubmitted(clock.UtcNow));
}
