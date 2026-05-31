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
        // Configuration JSON explicite pour éviter les comportements implicites de JsonSerializerDefaults.Web
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        // Enum converter avec allowIntegerValues pour rétrocompatibilité
        _json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        _json.Converters.Add(new GameIdJsonConverter());
        _json.Converters.Add(new PlayerIdJsonConverter());
        _json.Converters.Add(new CardIdJsonConverter());
        _json.Converters.Add(new ClueTextJsonConverter());
    }

    public async Task<Game?> Get(GameId id, CancellationToken ct = default)
    {
        var entity = await _db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id.Value, ct);
        if (entity is null) return null;
        try
        {
            return JsonSerializer.Deserialize<Game>(entity.PayloadJson, _json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] EfGameRepository ERROR deserializing game {id.Value}: {ex.Message}");
            throw;
        }
    }

    public async Task Save(Game game, CancellationToken ct = default)
    {
        var maxRetries = 5; // Augmenté de 3 à 5
        var delay = TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await UpsertInternal(game, ct);
                Console.WriteLine($"[DEBUG_LOG] EfGameRepository Saved game {game.Id.Value} in phase {game.Phase}. Attempt {attempt + 1}");
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                Console.WriteLine($"[DEBUG_LOG] EfGameRepository Concurrency conflict for game {game.Id.Value}. Retrying... (Attempt {attempt + 1})");
                await Task.Delay(delay + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200)), ct); // Jitter augmenté
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG_LOG] EfGameRepository ERROR saving game {game.Id.Value}: {ex.GetType().Name} - {ex.Message}");
                if (attempt >= maxRetries) throw;
                
                // On ajoute aussi un délai pour les autres erreurs, au cas où c'est un problème transitoire 
                // ou un conflit de tracking qui pourrait être résolu en réessayant avec un nouveau cycle.
                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task UpsertInternal(Game game, CancellationToken ct)
    {
        // On récupère l'entité AVEC tracking pour qu'EF puisse gérer la mise à jour et la concurrence correctement
        var entity = await _db.Games.FirstOrDefaultAsync(g => g.Id == game.Id.Value, ct);
        var now = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(game, _json);
        
        if (entity is null)
        {
            Console.WriteLine($"[DEBUG_LOG] EfGameRepository: Creating new GameEntity for {game.Id.Value}");
            entity = new GameEntity
            {
                Id = game.Id.Value,
                Status = game.Phase.ToString(),
                Language = game.Language,
                PhaseEndsAtUtc = game.PhaseEndsAtUtc,
                UpdatedAtUtc = now,
                PayloadJson = payload
            };
            _db.Games.Add(entity);
        }
        else
        {
            Console.WriteLine($"[DEBUG_LOG] EfGameRepository: Updating existing GameEntity for {game.Id.Value}");
            entity.Status = game.Phase.ToString();
            entity.Language = game.Language;
            entity.PhaseEndsAtUtc = game.PhaseEndsAtUtc;
            entity.UpdatedAtUtc = now;
            entity.PayloadJson = payload;
            
            // Pas besoin de appeler _db.Games.Update(entity) car l'entité est déjà trackée et modifiée
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (entity != null)
            {
                _db.Entry(entity).State = EntityState.Detached;
            }
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG_LOG] EfGameRepository: SaveChangesAsync ERROR: {ex.GetType().Name} - {ex.Message}");
            if (entity != null)
            {
                _db.Entry(entity).State = EntityState.Detached;
            }
            throw;
        }
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
        var games = new List<Game>();
        foreach (var e in list)
        {
            try
            {
                var game = JsonSerializer.Deserialize<Game>(e.PayloadJson, _json);
                if (game != null)
                {
                    games.Add(game);
                }
            }
            catch (JsonException ex)
            {
                // Log the error but skip corrupted game to avoid blocking the entire loop
                Console.WriteLine($"[ERROR] EfGameRepository.GetAll: Deserialization failed for game {e.Id}");
                Console.WriteLine($"[ERROR] JSON excerpt: {e.PayloadJson.Substring(0, Math.Min(200, e.PayloadJson.Length))}");
                Console.WriteLine($"[ERROR] Exception: {ex.Message}");
                // Continue to next game instead of failing
            }
        }
        return games;
    }
}

internal sealed class GameIdJsonConverter : JsonConverter<GameId>
{
    public override GameId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    // Support dictionary keys of type GameId
    public override GameId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, GameId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
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