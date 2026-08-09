using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// L'erreur de lecture du 2026-08-04 vient de l'affichage : <c>strict_2of2 0,000</c> ne disait
/// ni sur combien d'items il portait, ni combien en étaient à l'origine. Les effectifs rendent
/// la résolution de la métrique visible sans rien changer au registre.
/// </summary>
public class ScoreRateFormattingTests
{
    [Fact]
    public void Un_taux_est_suivi_de_son_numerateur_et_de_son_denominateur()
    {
        Assert.Equal("0,000   (0/22)", ScoreCommand.FormatRate(0.0, 0, 22));
    }

    [Fact]
    public void Le_taux_reste_en_virgule_decimale_francaise()
    {
        Assert.Equal("0,341   (15/44)", ScoreCommand.FormatRate(0.341, 15, 44));
    }

    // Un dénominateur nul ne doit pas s'afficher comme un taux de 0 : il n'y a rien à lire.
    [Fact]
    public void Un_denominateur_nul_le_dit_au_lieu_d_afficher_un_taux()
    {
        var rendered = ScoreCommand.FormatRate(0.0, 0, 0);

        Assert.Contains("0/0", rendered);
        Assert.DoesNotContain("0,000", rendered);
    }
}
