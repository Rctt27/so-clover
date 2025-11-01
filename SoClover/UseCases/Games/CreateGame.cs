using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.UseCases.Games;

public interface ICreateGameUseCase : IUseCase<CreateGame.Request, CreateGame.Response> { }

public static class CreateGame
{
    public readonly record struct Request(string PlayerName, string? Language = null);
    public readonly record struct Response(GameId GameId, PlayerId CreatorPlayerId);

    public sealed class Handler : ICreateGameUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IWordDictionary _wordDictionary;
        private readonly IClock _clock;
        private readonly IGameSettingsProvider _settings;

        public Handler(IGameRepository repo, IEventPublisher events, IWordDictionary wordDictionary, IClock clock, IGameSettingsProvider settings)
        {
            _repo = repo;
            _events = events;
            _wordDictionary = wordDictionary;
            _clock = clock;
            _settings = settings;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = new Game(GameId.New(), request.Language);
            await game.InitializeWordsPoolAsync(_wordDictionary, ct);

            // Create the admin player (game creator)
            var creatorPlayer = new Player(PlayerId.New(), request.PlayerName, isAdmin: true);
            game.AddPlayer(creatorPlayer);

            // Set Lobby deadline so lobbies don't stay open forever
            var now = _clock.UtcNow;
            var cfg = await _settings.GetAsync(ct);
            game.SetLobbyDeadline(now, TimeSpan.FromSeconds(cfg.LobbyDurationSeconds));

            await _repo.Save(game, ct);
            await _events.Publish(new GameCreated(game.Id, creatorPlayer.Id), ct);
            return new Response(game.Id, creatorPlayer.Id);
        }
    }
}

public readonly record struct GameCreated(GameId GameId, PlayerId CreatorPlayerId);
