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
}
