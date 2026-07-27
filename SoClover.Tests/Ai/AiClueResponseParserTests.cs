using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueResponseParserTests
{
    [Fact]
    public void ParseSingleDirection_accepts_the_canonical_mono_clue_shape()
    {
        const string json = """{"direction":"Top","clueWord":"Rivage","explanation":"plage et mer"}""";

        var draft = AiClueResponseParser.ParseSingleDirection(json);

        var clue = Assert.Single(draft.Clues);
        Assert.Equal("Top", clue.Direction);
        Assert.Equal("Rivage", clue.ClueWord);
    }

    [Fact]
    public void ParseSingleDirection_also_accepts_the_wrapped_clues_shape()
    {
        const string json = """{"clues":[{"direction":"Left","clueWord":"Pont","explanation":"x"}]}""";

        var draft = AiClueResponseParser.ParseSingleDirection(json);

        var clue = Assert.Single(draft.Clues);
        Assert.Equal("Left", clue.Direction);
    }

    [Fact]
    public void ParseSingleDirection_preserves_candidates()
    {
        const string json = """
        {"direction":"Top","clueWord":"Rivage","explanation":"x","candidates":["Rivage (fort, fort)"]}
        """;

        var draft = AiClueResponseParser.ParseSingleDirection(json);

        Assert.Equal(["Rivage (fort, fort)"], draft.Clues[0].Candidates);
    }

    [Fact]
    public void ParseSingleDirection_throws_a_typed_exception_on_unreadable_json()
    {
        var ex = Assert.Throws<UnparseableLlmResponseException>(
            () => AiClueResponseParser.ParseSingleDirection("ceci n'est pas du JSON"));

        Assert.Contains("ceci n'est pas du JSON", ex.RawTextExcerpt);
    }

    // Préserve le catch (InvalidOperationException) de GenerateAICluesPerDirection.cs:78.
    [Fact]
    public void UnparseableLlmResponseException_is_an_InvalidOperationException()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(
            new UnparseableLlmResponseException("bruit"));
    }

    [Fact]
    public void EmptyLlmResponseException_is_an_InvalidOperationException_and_carries_usage()
    {
        var ex = new EmptyLlmResponseException(
            Microsoft.Extensions.AI.ChatFinishReason.Length, inputTokens: 2100, outputTokens: 4096)
        {
            LatencyMs = 8123,
            PromptVersion = 5,
            EffectiveModel = "gemma-4-12b-qat",
        };

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Equal(Microsoft.Extensions.AI.ChatFinishReason.Length, ex.FinishReason);
        Assert.Equal(2100, ex.InputTokens);
        Assert.Equal(4096, ex.OutputTokens);
        Assert.Equal(8123, ex.LatencyMs);
        Assert.Equal(5, ex.PromptVersion);
        Assert.Equal("gemma-4-12b-qat", ex.EffectiveModel);
    }

    [Fact]
    public void UnparseableLlmResponseException_truncates_a_very_long_excerpt()
    {
        var ex = new UnparseableLlmResponseException(new string('x', 5_000));

        Assert.True(ex.RawTextExcerpt.Length <= UnparseableLlmResponseException.MaxExcerptLength);
    }

    [Fact]
    public void ParseBoard_reads_the_multi_clue_shape()
    {
        const string json = """
        {"clues":[{"direction":"Top","clueWord":"A","explanation":"x"},
                  {"direction":"Right","clueWord":"B","explanation":"y"}]}
        """;

        var draft = AiClueResponseParser.ParseBoard(json);

        Assert.Equal(2, draft.Clues.Count);
    }

    [Fact]
    public void ParseBoard_throws_a_typed_exception_on_unreadable_json()
    {
        Assert.Throws<UnparseableLlmResponseException>(() => AiClueResponseParser.ParseBoard("{"));
    }

    // Finding Important #2 : AiBoardCluesDraft est un record positionnel sans garde. "{}" et
    // {"clues":null} désérialisent tous deux vers un objet non-null dont Clues est null — le
    // `?? throw` du parser ne couvre que le retour null de la désérialisation elle-même, pas un
    // Clues null à l'intérieur d'un objet non-null. En aval, `foreach (var item in draft.Clues)`
    // lève un NullReferenceException non rattrapé (échappe au catch de retry ET au catch budget).
    [Fact]
    public void ParseBoard_rejects_an_empty_object()
    {
        Assert.Throws<UnparseableLlmResponseException>(() => AiClueResponseParser.ParseBoard("{}"));
    }

    [Fact]
    public void ParseBoard_rejects_a_null_clues_array()
    {
        Assert.Throws<UnparseableLlmResponseException>(
            () => AiClueResponseParser.ParseBoard("""{"clues":null}"""));
    }

    // Emprunte la branche "wrapped" de ParseSingleDirection (présence de la propriété "clues"),
    // avec une valeur null : même NullReferenceException latente en aval que ParseBoard_rejects_a_null_clues_array.
    [Fact]
    public void ParseSingleDirection_rejects_a_wrapped_object_with_null_clues()
    {
        Assert.Throws<UnparseableLlmResponseException>(
            () => AiClueResponseParser.ParseSingleDirection("""{"clues":null}"""));
    }
}
