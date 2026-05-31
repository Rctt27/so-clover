using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.Infrastructure.Persistence;
using SoClover.UseCases.Abstractions;

namespace SoClover.Tests.Helpers;

/// <summary>
/// Faithfully simulates EfGameRepository's JSON round-trip behavior without depending on EF Core.
/// Every Save serializes the Game to JSON; every Get deserializes it — so transient fields like
/// <c>_wordsPool</c> are lost, just like when loading from PostgreSQL.
/// </summary>
public sealed class JsonSerializingGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly JsonSerializerOptions _json;

    public JsonSerializingGameRepository()
    {
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        _json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        _json.Converters.Add(new GameIdJsonConverter());
        _json.Converters.Add(new PlayerIdJsonConverter());
        _json.Converters.Add(new CardIdJsonConverter());
        _json.Converters.Add(new ClueTextJsonConverter());
    }

    public Task<Game?> Get(GameId id, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id.Value, out var payload))
            return Task.FromResult<Game?>(null);

        var game = JsonSerializer.Deserialize<Game>(payload, _json)
            ?? throw new InvalidOperationException(
                $"JsonSerializingGameRepository: deserialization of game {id.Value} produced null — payload is corrupt.");
        return Task.FromResult<Game?>(game);
    }

    public Task Save(Game game, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(game, _json);
        _store[game.Id.Value] = payload;
        return Task.CompletedTask;
    }

    public Task Delete(GameId id, CancellationToken ct = default)
    {
        _store.TryRemove(id.Value, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Game>> GetAll(CancellationToken ct = default)
    {
        var games = _store.Values
            .Select(payload => JsonSerializer.Deserialize<Game>(payload, _json))
            .Where(g => g != null)
            .Cast<Game>()
            .ToList();
        return Task.FromResult<IReadOnlyList<Game>>(games);
    }
}
