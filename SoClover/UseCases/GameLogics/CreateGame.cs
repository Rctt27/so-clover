using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.UseCases.GameLogics;

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
        private readonly IWordsPoolCache _poolCache;
        private readonly IClock _clock;
        private readonly IGameSettingsProvider _settings;
        private readonly IGameCodeGenerator _codeGen;

        public Handler(IGameRepository repo, IEventPublisher events, IWordDictionary wordDictionary, IWordsPoolCache poolCache, IClock clock, IGameSettingsProvider settings, IGameCodeGenerator? codeGen = null)
        {
            _repo = repo;
            _events = events;
            _wordDictionary = wordDictionary;
            _poolCache = poolCache;
            _clock = clock;
            _settings = settings;
            // Fallback runtime/tests : générateur par défaut basé sur le dictionnaire déjà injecté
            // (UseCases -> Domain IWordDictionary, pas de dépendance Infrastructure).
            _codeGen = codeGen ?? new GameCodeGenerator(wordDictionary);
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            // Génère un code lisible 4-mots, unique vis-à-vis des parties existantes.
            GameId id;
            var attempts = 0;
            do
            {
                id = GameId.From(await _codeGen.GenerateAsync(ct));
            }
            while (await _repo.Get(id, ct) is not null && ++attempts < 5);

            var game = new Game(id, request.Language);
            var pool = await game.InitializeWordsPoolAsync(_wordDictionary, ct);
            _poolCache.Set(game.Id, pool);

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
