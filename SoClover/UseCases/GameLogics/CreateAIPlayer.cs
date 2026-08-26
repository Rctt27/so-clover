using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
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
        private readonly IOptions<AIPlayersOptions>? _aiPlayersOptions;
        private readonly IAiCluePromptProviderFactory? _promptProviderFactory;
        private readonly ILlmApiKeyProbe? _apiKeyProbe;

        public Handler(
            IGameRepository repo,
            IEventPublisher events,
            IOptions<GameDefaultsOptions> options,
            IOptions<AIPlayersOptions>? aiPlayersOptions = null,
            IAiCluePromptProviderFactory? promptProviderFactory = null,
            ILlmApiKeyProbe? apiKeyProbe = null)
        {
            _repo = repo;
            _events = events;
            _options = options;
            _aiPlayersOptions = aiPlayersOptions;
            _promptProviderFactory = promptProviderFactory;
            _apiKeyProbe = apiKeyProbe;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            if (_aiPlayersOptions is not null && !_aiPlayersOptions.Value.Enabled)
                throw new AIPlayersDisabledException();

            // Seconde porte : le flag peut être à true alors que la clé API vient d'être révoquée.
            // Sans ce contrôle, un client resté ouvert au-delà du TTL de la sonde ajouterait un bot
            // qui échouerait ensuite en pleine génération d'indices.
            if (_apiKeyProbe is not null && !await _apiKeyProbe.IsAvailableAsync(ct))
                throw AIPlayersDisabledException.ApiKeyUnavailable();

            var game = await _repo.Get(request.GameId, ct)
                ?? throw new GameNotFoundException(request.GameId);

            if (game.AdminPlayerId != request.AdminPlayerId)
                throw new UnauthorizedAccessException("Only the admin can create AI players.");

            if (_promptProviderFactory is not null && !_promptProviderFactory.IsLanguageSupported(game.Language))
                throw new UnsupportedAiLanguageException(game.Language);

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
