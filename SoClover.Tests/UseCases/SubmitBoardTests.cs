using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Gameplay;
using Xunit;

namespace SoClover.Tests.UseCases;

public class SubmitBoardRequestTests
{
    [Fact]
    public void Default_constructor_sets_Origin_to_Client_for_backward_compat()
    {
        var gameId = GameId.New();
        var playerId = PlayerId.New();

        var req = new SubmitBoard.Request(gameId, playerId);

        Assert.Equal(gameId, req.GameId);
        Assert.Equal(playerId, req.PlayerId);
        Assert.Equal(InvocationOrigin.Client, req.Origin);
    }

    [Fact]
    public void Explicit_constructor_with_System_origin_is_supported()
    {
        var gameId = GameId.New();
        var playerId = PlayerId.New();

        var req = new SubmitBoard.Request(gameId, playerId, InvocationOrigin.System);

        Assert.Equal(InvocationOrigin.System, req.Origin);
    }
}
