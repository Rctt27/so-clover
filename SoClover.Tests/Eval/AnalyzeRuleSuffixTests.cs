using SoClover.Eval.Analysis;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Le suffixe de chaque ligne de la distribution. Depuis I1, les directions D6 sortent du
/// numérateur ET du dénominateur des parts : la ligne <c>M?</c> ne les contient plus. Un suffixe
/// « dont N sans décodage » accroché à cette ligne annonçait donc un sous-ensemble plus grand que
/// l'ensemble — sur 6 directions D6 et 3 <c>M?</c> exploitables, la ligne se lisait
/// « 3 non classé — dont 6 sans décodage ».
/// </summary>
public class AnalyzeRuleSuffixTests
{
    private static ModeCount Mode(string mode, double share, bool actionJustified) =>
        new(mode, FailureModes.Label(mode), Count: 3, Share: share, ActionJustified: actionJustified);

    // L'effectif des D6 ne doit pas FUIR dans le libellé : il est rapporté en tête du rapport,
    // pas sur une ligne qui ne les contient plus. Deux effectifs différents, même suffixe.
    [Fact]
    public void La_ligne_M_interrogation_n_annonce_aucun_effectif_de_directions_sans_decodage()
    {
        var mode = Mode(FailureModes.Unclassified, 0.019, actionJustified: false);

        var suffix = AnalyzeCommand.RuleSuffix(mode, unscorableDirectionCount: 61);

        Assert.Equal(AnalyzeCommand.RuleSuffix(mode, unscorableDirectionCount: 7), suffix);
        Assert.DoesNotContain("61", suffix);
        Assert.DoesNotContain("dont", suffix);
    }

    // La part n'y est jamais une base d'action : ni « ≥ 5 % », ni « < 5 % » sur cette ligne.
    [Fact]
    public void La_regle_des_5_pourcent_ne_s_imprime_pas_sur_M_interrogation()
    {
        var suffix = AnalyzeCommand.RuleSuffix(
            Mode(FailureModes.Unclassified, 0.42, actionJustified: true), unscorableDirectionCount: 0);

        Assert.DoesNotContain("5 %", suffix);
    }

    [Fact]
    public void M5_n_est_jamais_extrapole()
    {
        var suffix = AnalyzeCommand.RuleSuffix(
            Mode(FailureModes.M5, 0.30, actionJustified: true), unscorableDirectionCount: 0);

        Assert.Contains("non extrapolé", suffix);
        Assert.DoesNotContain("5 %", suffix);
    }

    [Fact]
    public void M0_n_est_pas_un_mode_d_echec_et_ne_porte_aucune_regle()
    {
        Assert.Equal(
            string.Empty,
            AnalyzeCommand.RuleSuffix(
                Mode(FailureModes.M0, 0.60, actionJustified: true), unscorableDirectionCount: 0));
    }

    [Theory]
    [InlineData(FailureModes.M1)]
    [InlineData(FailureModes.M2)]
    [InlineData(FailureModes.M3)]
    [InlineData(FailureModes.M4)]
    public void Un_mode_semantique_porte_la_regle_des_5_pourcent(string mode)
    {
        Assert.Contains(
            "intervention justifiée",
            AnalyzeCommand.RuleSuffix(Mode(mode, 0.12, actionJustified: true), unscorableDirectionCount: 0));

        Assert.Contains(
            "aucune ligne de prompt",
            AnalyzeCommand.RuleSuffix(Mode(mode, 0.02, actionJustified: false), unscorableDirectionCount: 0));
    }
}
