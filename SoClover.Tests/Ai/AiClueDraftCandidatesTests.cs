using System.Text.Json;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class AiClueDraftCandidatesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Candidates_is_deserialized_when_present()
    {
        const string json = """
        {"direction":"Top","clueWord":"Hôpital","explanation":"lien médical",
         "candidates":["Hôpital (fort, fort)","Clinique (fort, moyen)"]}
        """;

        var draft = JsonSerializer.Deserialize<AiClueDraft>(json, Options)!;

        Assert.Equal(["Hôpital (fort, fort)", "Clinique (fort, moyen)"], draft.Candidates);
    }

    // Distinction volontaire : un modèle qui n'émet pas le champ (prompt EN, prompt v4) doit
    // rester discernable d'un modèle qui émet une liste vide.
    [Fact]
    public void Candidates_is_null_when_the_field_is_absent()
    {
        const string json = """{"direction":"Top","clueWord":"Rivage","explanation":"plage et mer"}""";

        var draft = JsonSerializer.Deserialize<AiClueDraft>(json, Options)!;

        Assert.Null(draft.Candidates);
    }

    [Fact]
    public void Candidates_is_an_empty_list_when_the_model_emits_an_empty_array()
    {
        const string json = """{"direction":"Top","clueWord":"Rivage","explanation":"x","candidates":[]}""";

        var draft = JsonSerializer.Deserialize<AiClueDraft>(json, Options)!;

        Assert.NotNull(draft.Candidates);
        Assert.Empty(draft.Candidates!);
    }

    [Fact]
    public void Wrapped_board_draft_carries_candidates_through()
    {
        const string json = """
        {"clues":[{"direction":"Top","clueWord":"Rivage","explanation":"x","candidates":["Rivage (fort, fort)"]}]}
        """;

        var draft = JsonSerializer.Deserialize<AiBoardCluesDraft>(json, Options)!;

        Assert.Equal(["Rivage (fort, fort)"], draft.Clues[0].Candidates);
    }

    [Fact]
    public void Three_argument_construction_still_compiles_and_leaves_candidates_null()
    {
        var draft = new AiClueDraft("Top", "Rivage", "plage et mer");

        Assert.Null(draft.Candidates);
    }
}
