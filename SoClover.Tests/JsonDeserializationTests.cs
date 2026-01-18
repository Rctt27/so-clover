using System.Text.Json;
using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.Infrastructure.Persistence;
using Xunit;

namespace SoClover.Tests;

public class JsonDeserializationTests
{
    [Fact]
    public void Should_Deserialize_Initial_GamePhase()
    {
        // Arrange
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter());
        
        // This simulates a minimal JSON from a persisted game where the phase is "Initial"
        var json = "{\"phase\": \"Initial\"}";

        // Act
        var result = JsonSerializer.Deserialize<PhaseWrapper>(json, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(GamePhase.Initial, result.Phase);
    }

    private class PhaseWrapper
    {
        [JsonPropertyName("phase")]
        public GamePhase Phase { get; set; }
    }

    [Fact]
    public void Should_Deserialize_Full_Game_With_Initial_Phase()
    {
        // Arrange
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter());
        // We need the converters from EfGameRepository for GameId, PlayerId etc.
        // But since they are internal, we'll manually add them or test with the Repo logic if possible.
        // For this test, let's just use the same logic as EfGameRepository constructor.
    }
}
