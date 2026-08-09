using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// La colonne « notes » du registre est le seul endroit où le modèle décodeur et le gotcha
/// « thinking OFF » deviennent visibles. Sans elle, deux lignes décodées par des modèles
/// différents sont incomparables sans que rien ne le signale.
/// </summary>
public class ScoreCommandNotesTests
{
    private static DecodeContents Decoded(string modelId, string? operatorNotes) =>
        new(new DecodeManifest(
                "manifest", "d", DateTime.UtcNow, "r", "eval/boards.dev.jsonl", "a1b2c3d4e5f6",
                "OpenAI", "u", modelId, null, null, 0.3, null, 512,
                "clue.md", 1, "board.md", 1, 3, RunFile.HarnessVersion, operatorNotes),
            [], []);

    [Fact]
    public void Without_a_decode_the_run_notes_pass_through_untouched()
    {
        Assert.Equal("thinking OFF, ctx 16k", ScoreCommand.ComposeNotes("thinking OFF, ctx 16k", null));
    }

    [Fact]
    public void Without_a_decode_and_without_run_notes_there_is_nothing_to_say()
    {
        Assert.Null(ScoreCommand.ComposeNotes(null, null));
    }

    // Le point décisif : un recovery sans nom de décodeur n'est pas interprétable.
    [Fact]
    public void A_decode_always_names_the_decoder_model_even_with_no_notes_anywhere()
    {
        var notes = ScoreCommand.ComposeNotes(null, Decoded("qwen/qwen3-8b", null));

        Assert.Contains("qwen/qwen3-8b", notes);
    }

    [Fact]
    public void Generator_and_decoder_notes_are_both_kept_and_attributed()
    {
        var notes = ScoreCommand.ComposeNotes(
            "générateur thinking OFF, ctx 16k",
            Decoded("qwen/qwen3-8b", "décodeur thinking OFF, ctx 8k"));

        Assert.Contains("générateur thinking OFF, ctx 16k", notes);
        Assert.Contains("qwen/qwen3-8b", notes);
        Assert.Contains("décodeur thinking OFF, ctx 8k", notes);
        Assert.Contains("gén.", notes);
        Assert.Contains("déc.", notes);
    }

    // Le pipe serait neutralisé par LedgerWriter : le séparateur ne doit pas en être un,
    // sinon la fusion deviendrait illisible dans la colonne.
    [Fact]
    public void The_separator_is_not_a_pipe()
    {
        var notes = ScoreCommand.ComposeNotes("a", Decoded("m", "b"));

        Assert.DoesNotContain("|", notes);
    }
}
