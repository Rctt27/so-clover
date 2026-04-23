using System.Text.Json;
using System.Text.Json.Serialization;
using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class AiPlayerDomainTests
{
    /// <summary>
    /// Crée des options JSON identiques à celles d'EfGameRepository.
    /// CRITICAL: toute divergence ici fausserait les tests de round-trip.
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return options;
    }

    [Fact]
    public void AIConfig_round_trip_JSON_preserves_model_and_temperature()
    {
        var config = new AIConfig("gpt-4o-mini", 0.7);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(config, options);
        var deserialized = JsonSerializer.Deserialize<AIConfig>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal("gpt-4o-mini", deserialized!.Model);
        Assert.Equal(0.7, deserialized.Temperature);
    }

    [Fact]
    public void Player_default_constructor_creates_human_player()
    {
        var player = new Player(PlayerId.New(), "Alice");

        Assert.False(player.IsAI);
        Assert.Null(player.AIConfig);
    }

    [Fact]
    public void Player_AI_constructor_sets_IsAI_and_AIConfig()
    {
        var config = new AIConfig("gpt-4o-mini", 0.7);
        var player = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: config);

        Assert.True(player.IsAI);
        Assert.NotNull(player.AIConfig);
        Assert.Equal("gpt-4o-mini", player.AIConfig!.Model);
        Assert.Equal(0.7, player.AIConfig.Temperature);
    }

    [Fact]
    public void Player_AI_round_trip_JSON_preserves_IsAI_and_AIConfig()
    {
        var config = new AIConfig("claude-haiku-4-5", 0.5);
        var original = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: config);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(original, options);
        var rehydrated = JsonSerializer.Deserialize<Player>(json, options);

        Assert.NotNull(rehydrated);
        Assert.True(rehydrated!.IsAI);
        Assert.NotNull(rehydrated.AIConfig);
        Assert.Equal("claude-haiku-4-5", rehydrated.AIConfig!.Model);
        Assert.Equal(0.5, rehydrated.AIConfig.Temperature);
        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Name, rehydrated.Name);
    }

    [Fact]
    public void Player_human_round_trip_JSON_keeps_IsAI_false_and_AIConfig_null()
    {
        var original = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(original, options);
        var rehydrated = JsonSerializer.Deserialize<Player>(json, options);

        Assert.NotNull(rehydrated);
        Assert.False(rehydrated!.IsAI);
        Assert.Null(rehydrated.AIConfig);
    }

    [Fact]
    public void GuessingParticipants_excludes_AI_players()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(human);
        game.AddPlayer(bot);

        Assert.Single(game.GuessingParticipants);
        Assert.Contains(game.GuessingParticipants, p => p.Id == human.Id);
        Assert.DoesNotContain(game.GuessingParticipants, p => p.Id == bot.Id);
    }

    [Fact]
    public void GuessingParticipants_excludes_disconnected_humans()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice");
        var bob = new Player(PlayerId.New(), "Bob");
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        bob.MarkDisconnected();

        Assert.Single(game.GuessingParticipants);
        Assert.Contains(game.GuessingParticipants, p => p.Id == alice.Id);
    }

    [Fact]
    public void GuessingParticipants_is_empty_when_only_AIs_and_disconnected_remain()
    {
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(admin);
        game.AddPlayer(bot);
        admin.MarkDisconnected();

        Assert.Empty(game.GuessingParticipants);
    }
}
