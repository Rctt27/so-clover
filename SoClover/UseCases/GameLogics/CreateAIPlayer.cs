using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.GameLogics;

public interface ICreateAIPlayerUseCase : IUseCase<CreateAIPlayer.Request, CreateAIPlayer.Response> { }

public static class CreateAIPlayer
{
    public readonly record struct Request(
        GameId GameId,
        PlayerId AdminPlayerId,
        string PlayerName,
        string? Model,
        double? Temperature);

    public readonly record struct Response(PlayerId PlayerId);

    public sealed class Handler : ICreateAIPlayerUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IOptions<GameDefaultsOptions> _options;

        public Handler(IGameRepository repo, IEventPublisher events, IOptions<GameDefaultsOptions> options)
        {
            _repo = repo;
            _events = events;
            _options = options;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct)
                ?? throw new GameNotFoundException(request.GameId);

            if (game.AdminPlayerId != request.AdminPlayerId)
                throw new UnauthorizedAccessException("Only the admin can create AI players.");

            AIConfig? aiConfig = null;
            if (request.Model is not null && request.Temperature.HasValue)
                aiConfig = new AIConfig(request.Model, request.Temperature.Value);

            var aiPlayer = new Player(
                PlayerId.New(),
                request.PlayerName,
                isAdmin: false,
                isAI: true,
                aiConfig: aiConfig);

            var max = _options.Value.MaxAIPlayersPerGame > 0 ? _options.Value.MaxAIPlayersPerGame : 4;
            game.AddAIPlayer(aiPlayer, max);

            await _repo.Save(game, ct);
            await _events.Publish(new PlayerJoined(game.Id, aiPlayer.Id), ct);

            return new Response(aiPlayer.Id);
        }
    }
}
