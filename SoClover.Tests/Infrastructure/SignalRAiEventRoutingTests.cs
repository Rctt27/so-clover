using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.RealTime;
using SoClover.UseCases.AI;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests.Infrastructure;

public class SignalRAiEventRoutingTests
{
    private static (SignalREventPublisher Publisher,
                    Mock<IClientProxy> ClientProxy,
                    GameId GameId,
                    PlayerId PlayerId)
        Build()
    {
        var inner = new InMemoryEventPublisher();
        var hubMock = new Mock<IHubContext<GameHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        hubMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var gameId = GameId.New();
        var playerId = PlayerId.New();
        var stateUseCase = new Mock<IGetGameStateUseCase>();
        stateUseCase
            .Setup(s => s.Handle(It.IsAny<GetGameState.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetGameState.Response(
                GameId: gameId.Value,
                Language: "Français_OFF",
                CluesDurationSecondsOverride: null,
                GuessDurationSecondsOverride: null,
                SemanticClueCheckEnabled: false,
                GuessAiBoardOnly: false,
                Phase: GamePhase.WritingClues,
                AdminPlayerId: playerId.Value,
                PhaseEndsAtUtc: null,
                Revision: 0,
                Players: Array.Empty<GetGameState.PlayerState>(),
                GuessingState: null));

        var services = new ServiceCollection();
        services.AddScoped<IGetGameStateUseCase>(_ => stateUseCase.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var publisher = new SignalREventPublisher(inner, hubMock.Object, scopeFactory);
        return (publisher, clientProxyMock, gameId, playerId);
    }

    [Fact]
    public async Task AiClueGenerated_routes_specific_message_to_game_group()
    {
        var (publisher, clientProxy, gameId, playerId) = Build();
        var evt = new AiClueGenerated(gameId, playerId, Direction.Top, "lune", "ciel + nuit");

        await publisher.Publish(evt);

        clientProxy.Verify(c => c.SendCoreAsync(
            "AiClueGenerated",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiClueGenerationFailed_routes_specific_message_to_game_group()
    {
        var (publisher, clientProxy, gameId, playerId) = Build();
        var evt = new AiClueGenerationFailed(
            gameId, playerId, Direction.Left, "no valid clue", new[] { "boom" });

        await publisher.Publish(evt);

        clientProxy.Verify(c => c.SendCoreAsync(
            "AiClueGenerationFailed",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiClueGenerationRequested_routes_specific_message_to_game_group()
    {
        var (publisher, clientProxy, gameId, playerId) = Build();
        var evt = new AiClueGenerationRequested(gameId, playerId);

        await publisher.Publish(evt);

        clientProxy.Verify(c => c.SendCoreAsync(
            "AiClueGenerationRequested",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
