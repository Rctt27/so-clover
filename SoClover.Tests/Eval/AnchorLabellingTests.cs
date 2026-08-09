using SoClover.Eval.Calibration;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Deux compteurs d'ancres, deux noms — même geste que les trois compteurs de couples
/// (<c>CoupleCount</c> / <c>ScorableCoupleCount</c> / <c>CoupleAndAnchorCount</c>).
/// <para>
/// La sortie de <c>calibrate</c> du 2026-08-06 imprimait <c>ancres 5 / 5</c> immédiatement
/// au-dessus de <c>⚠ LOT SUSPECT (ancres ratées)</c>. Les deux lignes se contredisent à la
/// lecture, et rien ne disait que la première compte les ancres du <b>décodeur</b> quand la
/// seconde découle de celles du <b>juge</b> (3/5). Un lecteur du registre, six mois plus tard,
/// n'a aucun moyen de le deviner.
/// </para>
/// </summary>
public class AnchorLabellingTests
{
    [Fact]
    public void Les_deux_libelles_nomment_leur_sujet_et_se_distinguent()
    {
        Assert.Contains("décodeur", CalibrateCommand.DecoderAnchorLabel, StringComparison.Ordinal);
        Assert.Contains("juge", CalibrateCommand.JudgeAnchorLabel, StringComparison.Ordinal);
        Assert.NotEqual(CalibrateCommand.DecoderAnchorLabel, CalibrateCommand.JudgeAnchorLabel);
    }

    /// <summary>
    /// Le cas réel qui a motivé le correctif : décodeur 5/5, juge 3/5, lot suspect.
    /// </summary>
    [Fact]
    public void Chaque_score_est_attache_a_son_porteur()
    {
        var lines = CalibrateCommand.AnchorLines(
            decoderCorrect: 5, decoderCount: 5, judgeCorrect: 3, judgeCount: 5);

        var decoder = Assert.Single(lines, l => l.StartsWith(CalibrateCommand.DecoderAnchorLabel, StringComparison.Ordinal));
        var judge = Assert.Single(lines, l => l.StartsWith(CalibrateCommand.JudgeAnchorLabel, StringComparison.Ordinal));

        Assert.Contains("5 / 5", decoder, StringComparison.Ordinal);
        Assert.Contains("3 / 5", judge, StringComparison.Ordinal);
    }

    /// <summary>
    /// Le score du décodeur ne participe à aucune porte : il diagnostique l'instrument, il ne le
    /// juge pas. La mention doit rester attachée à sa ligne, pas flotter dans le bloc.
    /// </summary>
    [Fact]
    public void La_ligne_du_decodeur_rappelle_qu_elle_est_hors_calcul()
    {
        var lines = CalibrateCommand.AnchorLines(5, 5, 3, 5);
        var decoder = lines.Single(l => l.StartsWith(CalibrateCommand.DecoderAnchorLabel, StringComparison.Ordinal));

        Assert.Contains("hors calcul principal", decoder, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'invariant, énoncé directement : toute ligne d'ancres nomme son porteur <b>avant</b> son
    /// chiffre. C'est ce qui manquait — le chiffre arrivait nu, et le lecteur l'attribuait au
    /// mauvais des deux.
    /// </summary>
    [Fact]
    public void Toute_ligne_nomme_son_porteur_avant_son_chiffre()
    {
        var lines = CalibrateCommand.AnchorLines(5, 5, 3, 5);

        Assert.All(lines, line =>
        {
            var firstDigit = line.IndexOfAny("0123456789".ToCharArray());
            Assert.True(firstDigit > 0, $"Ligne sans chiffre : « {line} »");

            var label = line[..firstDigit];
            Assert.True(
                label.Contains("décodeur", StringComparison.Ordinal)
                || label.Contains("juge", StringComparison.Ordinal),
                $"Libellé ambigu — on ne sait pas de qui parle le chiffre : « {line} »");
        });
    }
}
