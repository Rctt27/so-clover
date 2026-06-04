using SoClover.Domain;
using SoClover.RealTime;
using Xunit;

namespace SoClover.Tests;

public class GracePeriodPolicyTests
{
    [Fact]
    public void Lobby_grace_is_longer_than_in_game_grace()
    {
        Assert.True(GracePeriodPolicy.LobbyGraceSeconds > GracePeriodPolicy.InGameGraceSeconds);
    }

    [Fact]
    public void SecondsForPhase_returns_lobby_grace_in_lobby()
    {
        Assert.Equal(GracePeriodPolicy.LobbyGraceSeconds, GracePeriodPolicy.SecondsForPhase(GamePhase.Lobby));
    }

    [Theory]
    [InlineData(GamePhase.WritingClues)]
    [InlineData(GamePhase.Guessing)]
    [InlineData(GamePhase.Scoring)]
    public void SecondsForPhase_returns_in_game_grace_outside_lobby(GamePhase phase)
    {
        Assert.Equal(GracePeriodPolicy.InGameGraceSeconds, GracePeriodPolicy.SecondsForPhase(phase));
    }
}
