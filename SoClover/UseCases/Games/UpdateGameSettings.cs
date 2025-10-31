using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public interface IUpdateGameSettingsUseCase : IUseCase<UpdateGameSettings.Request, UpdateGameSettings.Response> { }

public static class UpdateGameSettings
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, string Language);
    public readonly record struct Response(string Language);

    public sealed class Handler : IUpdateGameSettingsUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IWordDictionary _wordDictionary;

        public Handler(IGameRepository repo, IWordDictionary wordDictionary)
        {
            _repo = repo;
            _wordDictionary = wordDictionary;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);

            // Only admin can update settings
            if (game.AdminPlayerId != request.PlayerId)
            {
                throw new UnauthorizedAccessException("Only the admin can update game settings.");
            }

            await game.UpdateLanguageAsync(request.Language, _wordDictionary, ct);
            await _repo.Save(game, ct);

            return new Response(game.Language);
        }
    }
}
