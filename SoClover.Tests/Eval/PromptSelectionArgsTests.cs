using SoClover.Eval.Cli;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using Xunit;

namespace SoClover.Tests.Eval;

public class PromptSelectionArgsTests
{
    private static readonly LangfuseOptions Defaults = new();

    private static PromptSelection From(params string[] argv) =>
        PromptSelectionArgs.From(Args.Parse(["decode", .. argv]), Defaults);

    [Fact]
    public void Sans_drapeau_la_configuration_decide_source_langfuse_et_label_production()
    {
        Assert.Equal(new PromptSelection(PromptSource.Langfuse, "production", null), From());
    }

    [Fact]
    public void La_configuration_peut_designer_la_source_fichier()
    {
        var selection = PromptSelectionArgs.From(Args.Parse(["decode"]), new LangfuseOptions { PromptSource = PromptSource.File });

        Assert.Equal(new PromptSelection(PromptSource.File, null, null), selection);
    }

    [Fact]
    public void Le_drapeau_de_source_prime_sur_la_configuration()
    {
        Assert.Equal(PromptSource.File, From("--prompt-source", "file").Source);
    }

    [Fact]
    public void Un_label_explicite_remplace_le_label_par_defaut()
    {
        Assert.Equal(new PromptSelection(PromptSource.Langfuse, "candidat", null), From("--prompt-label", "candidat"));
    }

    [Fact]
    public void Une_version_explicite_efface_le_label()
    {
        Assert.Equal(new PromptSelection(PromptSource.Langfuse, null, 7), From("--prompt-version", "7"));
    }

    [Theory]
    [InlineData("--prompt-source", "git")]
    [InlineData("--prompt-version", "sept")]
    public void Une_valeur_hors_vocabulaire_est_refusee(string flag, string value)
    {
        Assert.Throws<ArgumentException>(() => From(flag, value));
    }

    [Fact]
    public void Label_et_version_s_excluent()
    {
        Assert.Throws<ArgumentException>(() => From("--prompt-label", "candidat", "--prompt-version", "7"));
    }

    [Fact]
    public void Label_ou_version_avec_la_source_fichier_sont_refuses()
    {
        Assert.Throws<ArgumentException>(() => From("--prompt-source", "file", "--prompt-label", "candidat"));
    }

    /// <summary>
    /// `decode` résout deux prompts. `--prompt-version` désigne le prompt clue — celui qui porte
    /// l'empreinte ; le prompt board suit le label (explicite ou par défaut).
    /// </summary>
    [Fact]
    public void Le_prompt_board_suit_le_label_meme_quand_le_clue_est_designe_par_version()
    {
        Assert.Equal(new PromptSelection(PromptSource.Langfuse, "production", null),
            PromptSelectionArgs.ForBoardPrompt(From("--prompt-version", "7"), Defaults));
        Assert.Equal(new PromptSelection(PromptSource.Langfuse, "candidat", null),
            PromptSelectionArgs.ForBoardPrompt(From("--prompt-label", "candidat"), Defaults));
        Assert.Equal(new PromptSelection(PromptSource.File, null, null),
            PromptSelectionArgs.ForBoardPrompt(From("--prompt-source", "file"), Defaults));
    }
}
