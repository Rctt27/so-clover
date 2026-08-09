using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Il y a <b>deux</b> prompts décodeurs, versionnés séparément — <c>decode-clue</c> (N2) et
/// <c>decode-board</c> (N3) — et une seule alerte les confondait.
/// <para>
/// Constat du 2026-08-06, deux fois de suite : sur le plancher aléatoire décodé par
/// <c>ministral-3-14b-reasoning</c>, le taux agrégé valait 0,085 puis 0,094 et l'alerte annonçait
/// « le prompt décodeur est cassé, aucun recovery n'est lisible ». Faux les deux fois : N2 tenait
/// à 3,7 % puis 4,2 %, <b>tous <c>outOfVocabulary</c>, zéro <c>unparseable</c></b>, et c'est N3
/// qui décrochait (65 %, puis 72,5 %). Le diagnostic a dû être refait à la main à chaque run.
/// </para>
/// <para>
/// L'enjeu n'est pas cosmétique : N2 porte les <b>quatre portes de calibration</b>, N3 porte
/// <c>board_positions</c> et <c>M6</c>. Annoncer « aucun recovery n'est lisible » quand seul le
/// board décroche invite à jeter une calibration parfaitement valide.
/// </para>
/// </summary>
public class DecodeFailureWarningTests
{
    private static MetricCounts Counts(int clueDecodes, int clueFailures, int boardDecodes, int boardFailures) =>
        MetricCounts.Zero with
        {
            ClueDecodes = clueDecodes,
            ClueDecodeFailures = clueFailures,
            BoardDecodes = boardDecodes,
            BoardDecodeFailures = boardFailures,
            Decodes = clueDecodes + boardDecodes,
            DecodeFailures = clueFailures + boardFailures,
        };

    /// <summary>Le cas réel : N2 sain, N3 cassé, taux agrégé au-dessus du seuil.</summary>
    [Fact]
    public void Le_board_casse_n_accuse_pas_le_prompt_indice()
    {
        var warnings = ScoreCommand.DecodeFailureWarnings(Counts(480, 20, 40, 29));

        var line = Assert.Single(warnings);
        Assert.Contains("decode-board", line, StringComparison.Ordinal);
        Assert.DoesNotContain("decode-clue", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Et il dit explicitement ce qui reste lisible. Sans cette phrase, le lecteur retient
    /// « décodeur cassé » et jette la calibration.
    /// </summary>
    [Fact]
    public void L_alerte_board_precise_que_le_recovery_n_est_pas_affecte()
    {
        var line = Assert.Single(ScoreCommand.DecodeFailureWarnings(Counts(480, 20, 40, 29)));

        Assert.Contains("recovery", line, StringComparison.Ordinal);
        Assert.Contains("board_positions", line, StringComparison.Ordinal);
    }

    [Fact]
    public void L_indice_casse_declenche_sa_propre_alerte()
    {
        var warnings = ScoreCommand.DecodeFailureWarnings(Counts(480, 100, 40, 1));

        var line = Assert.Single(warnings);
        Assert.Contains("decode-clue", line, StringComparison.Ordinal);
        Assert.DoesNotContain("decode-board", line, StringComparison.Ordinal);
    }

    /// <summary>Les deux peuvent tomber ensemble : deux lignes, chacune nommant son prompt.</summary>
    [Fact]
    public void Les_deux_prompts_casses_donnent_deux_alertes_distinctes()
    {
        var warnings = ScoreCommand.DecodeFailureWarnings(Counts(480, 100, 40, 29));

        Assert.Equal(2, warnings.Count);
        Assert.Single(warnings, w => w.Contains("decode-clue", StringComparison.Ordinal));
        Assert.Single(warnings, w => w.Contains("decode-board", StringComparison.Ordinal));
    }

    [Fact]
    public void Aucune_alerte_quand_les_deux_prompts_tiennent()
    {
        Assert.Empty(ScoreCommand.DecodeFailureWarnings(Counts(480, 3, 40, 1)));
    }

    /// <summary>
    /// Le taux agrégé ne déclenche plus rien par lui-même. C'est tout l'objet du correctif :
    /// ici 24/520 = 4,6 % globalement, mais N3 est à 50 % et doit être signalé.
    /// </summary>
    [Fact]
    public void Le_taux_agrege_ne_gouverne_plus_le_declenchement()
    {
        var warnings = ScoreCommand.DecodeFailureWarnings(Counts(480, 4, 40, 20));

        var line = Assert.Single(warnings);
        Assert.Contains("decode-board", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Un dénominateur nul ne fabrique pas de taux — même règle que <c>FormatRate</c> : un board
    /// jamais décodé n'est pas un board cassé.
    /// </summary>
    [Fact]
    public void Un_denominateur_nul_ne_declenche_aucune_alerte()
    {
        Assert.Empty(ScoreCommand.DecodeFailureWarnings(Counts(0, 0, 0, 0)));
        Assert.Empty(ScoreCommand.DecodeFailureWarnings(Counts(480, 3, 0, 0)));
    }
}
