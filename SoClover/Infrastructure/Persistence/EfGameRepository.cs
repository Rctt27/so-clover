using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure.Persistence;

public sealed class EfGameRepository : IGameRepository
{
    private readonly GameDbContext _db;
    private readonly JsonSerializerOptions _json;

    public EfGameRepository(GameDbContext db)
    {
        _db = db;
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        _json.Converters.Add(new JsonStringEnumConverter());
        _json.Converters.Add(new GameIdJsonConverter());
        _json.Converters.Add(new PlayerIdJsonConverter());
        _json.Converters.Add(new CardIdJsonConverter());
        _json.Converters.Add(new ClueTextJsonConverter());
    }

    public async Task<Game?> Get(GameId id, CancellationToken ct = default)
    {
        var entity = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id.Value, ct);
        if (entity is null) return null;
        return JsonSerializer.Deserialize<Game>(entity.PayloadJson, _json);
    }

    public async Task Save(Game game, CancellationToken ct = default)
    {
        var maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await UpsertInternal(game, ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                // Reload and retry with backoff
                await Task.Delay(delay + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)), ct);
            }
        }
    }

    private async Task UpsertInternal(Game game, CancellationToken ct)
    {
        var entity = await _db.Games.FirstOrDefaultAsync(g => g.Id == game.Id.Value, ct);
        var now = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(game, _json);
        if (entity is null)
        {
            entity = new GameEntity
            {
                Id = game.Id.Value,
                Status = game.Phase.ToString(),
                Language = game.Language,
                PhaseEndsAtUtc = game.PhaseEndsAtUtc,
                UpdatedAtUtc = now,
                PayloadJson = payload
                // xmin handled by database
            };
            _db.Games.Add(entity);
        }
        else
        {
            entity.Status = game.Phase.ToString();
            entity.Language = game.Language;
            entity.PhaseEndsAtUtc = game.PhaseEndsAtUtc;
            entity.UpdatedAtUtc = now;
            entity.PayloadJson = payload;
            _db.Entry(entity).State = EntityState.Modified;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task Delete(GameId id, CancellationToken ct = default)
    {
        var entity = await _db.Games.FirstOrDefaultAsync(g => g.Id == id.Value, ct);
        if (entity is null) return;
        _db.Games.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Game>> GetAll(CancellationToken ct = default)
    {
        var list = await _db.Games.AsNoTracking().ToListAsync(ct);
        return list.Select(e => JsonSerializer.Deserialize<Game>(e.PayloadJson, _json)!).ToList();
    }
}

internal sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    // Support dictionary keys of type GameId
    public override GameId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(Guid.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString());
}

internal sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    // Support dictionary keys of type PlayerId
    public override PlayerId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(Guid.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString());
}

internal sealed class CardIdJsonConverter : JsonConverter<CardId>
{
    public override CardId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, CardId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    // Support dictionary keys of type CardId
    public override CardId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(Guid.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, CardId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString());
}

internal sealed class ClueTextJsonConverter : JsonConverter<ClueText>
{
    public override ClueText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            // For nullable ClueText?, the serializer will handle nulls before invoking this converter.
            // Returning default is safe for non-nullable paths when data is malformed.
            return default;
        }
        var str = reader.GetString();
        return ClueText.Create(str);
    }

    public override void Write(Utf8JsonWriter writer, ClueText value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}