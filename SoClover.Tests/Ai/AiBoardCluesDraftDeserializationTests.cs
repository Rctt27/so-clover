using System.Text.Json;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class AiBoardCluesDraftDeserializationTests
{
    [Fact]
    public void Deserialize_full_response_returns_4_clues_with_directions()
    {
        const string json = """
        {
          "clues": [
            { "direction": "Top",    "clueWord": "soleil", "explanation": "Mer + plage" },
            { "direction": "Right",  "clueWord": "orage",  "explanation": "Pluie + foudre" },
            { "direction": "Bottom", "clueWord": "noir",   "explanation": "Nuit + ombre" },
            { "direction": "Left",   "clueWord": "feu",    "explanation": "Chaud + flamme" }
          ]
        }
        """;

        var draft = JsonSerializer.Deserialize<AiBoardCluesDraft>(json, JsonOptions);

        Assert.NotNull(draft);
        Assert.Equal(4, draft!.Clues.Count);
        Assert.Equal("Top", draft.Clues[0].Direction);
        Assert.Equal("soleil", draft.Clues[0].ClueWord);
        Assert.Equal("Mer + plage", draft.Clues[0].Explanation);
    }

    [Fact]
    public void Deserialize_partial_response_returns_2_clues()
    {
        const string json = """
        { "clues": [
            { "direction": "Top",   "clueWord": "soleil", "explanation": "..." },
            { "direction": "Right", "clueWord": "orage",  "explanation": "..." }
        ] }
        """;

        var draft = JsonSerializer.Deserialize<AiBoardCluesDraft>(json, JsonOptions);

        Assert.NotNull(draft);
        Assert.Equal(2, draft!.Clues.Count);
    }

    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
