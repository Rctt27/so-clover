using SoClover.Domain;
using SoClover.RealTime;
using Xunit;

namespace SoClover.Tests;

public class DisconnectGraceDecisionTests
{
    [Fact]
    public void Lobby_admin_is_never_auto_removed()
    {
        Assert.Equal(GraceAction.None, DisconnectGraceDecision.Decide(GamePhase.Lobby, isAdmin: true));
    }

    [Fact]
    public void Lobby_non_admin_leaves_game()
    {
        Assert.Equal(GraceAction.LeaveGame, DisconnectGraceDecision.Decide(GamePhase.Lobby, isAdmin: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WritingClues_disconnects_player(bool isAdmin)
    {
        Assert.Equal(GraceAction.DisconnectPlayer, DisconnectGraceDecision.Decide(GamePhase.WritingClues, isAdmin));
    }

    [Theory]
    [InlineData(GamePhase.Guessing)]
    [InlineData(GamePhase.Scoring)]
    public void Other_phases_do_nothing(GamePhase phase)
    {
        Assert.Equal(GraceAction.None, DisconnectGraceDecision.Decide(phase, isAdmin: false));
    }
}
