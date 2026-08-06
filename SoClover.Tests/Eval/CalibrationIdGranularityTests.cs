using SoClover.Eval.Calibration;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// <c>decodesPerClue</c> est <b>hors empreinte</b> par construction — il change la granularité de
/// R̄, pas le décodeur. Mais il était aussi absent du <b>nom</b> des artefacts de calibration, qui
/// ne portait que la date et l'empreinte.
/// <para>
/// Conséquence rencontrée le 2026-08-06 : après avoir calibré <c>27e36fefe975</c> à 9 décodages, il
/// devenait impossible de recalibrer <b>le même décodeur</b> à 5 décodages pour départager l'effet
/// de <c>topP</c> de celui de la granularité — même date et même empreinte donnent le même chemin,
/// et <c>RequireCompatibleResume</c> refuse à juste titre puisque <c>--decodes</c> diverge. Le seul
/// contournement était <c>--force</c>, qui écrase 1 620 décodages déjà payés.
/// </para>
/// <para>
/// C'est le pendant exact du correctif appliqué aux décodages (<c>a8e74b3</c>) : deux mesures
/// distinctes sont deux artefacts, pas deux versions d'un seul.
/// </para>
/// </summary>
public class CalibrationIdGranularityTests
{
    private const string Fingerprint = "27e36fefe975";

    [Fact]
    public void Deux_granularites_du_meme_decodeur_donnent_deux_identifiants()
    {
        var five = CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, decodesPerClue: 5);
        var nine = CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, decodesPerClue: 9);

        Assert.NotEqual(five, nine);
    }

    [Fact]
    public void Deux_granularites_du_meme_decodeur_donnent_deux_chemins()
    {
        var five = CalibrationFile.PathFor("eval/human", CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, 5));
        var nine = CalibrationFile.PathFor("eval/human", CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, 9));

        Assert.NotEqual(five, nine);
        Assert.EndsWith(".jsonl", five, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'empreinte reste lisible telle quelle dans le nom : c'est elle qui dit si deux
    /// <c>recovery</c> se comparent, et on la relit à l'œil dans <c>eval/human/</c>.
    /// </summary>
    [Fact]
    public void L_identifiant_porte_la_date_l_empreinte_et_la_granularite()
    {
        var id = CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, decodesPerClue: 9);

        Assert.Contains("20260806", id, StringComparison.Ordinal);
        Assert.Contains(Fingerprint, id, StringComparison.Ordinal);
        Assert.Contains("9", id, StringComparison.Ordinal);
    }

    /// <summary>
    /// La granularité vient <b>après</b> l'empreinte, jamais entre la date et elle : les six
    /// calibrations déjà committées se trient par date puis par décodeur dans <c>eval/human/</c>,
    /// et cet ordre de lecture doit survivre à l'ajout.
    /// </summary>
    [Fact]
    public void La_granularite_ne_s_intercale_pas_avant_l_empreinte()
    {
        var id = CalibrateCommand.CalibrationIdFor("20260806", Fingerprint, decodesPerClue: 9);

        Assert.StartsWith($"20260806-{Fingerprint}", id, StringComparison.Ordinal);
    }

    /// <summary>
    /// Les six artefacts antérieurs à la convention (<c>calibration.20260806-9a829dc206d2.json</c>)
    /// coûtent des milliers d'appels LLM et sont committés. Ils restent lisibles : seule la
    /// <b>fabrication</b> du nom change, jamais la lecture.
    /// </summary>
    [Fact]
    public void Les_artefacts_anterieurs_a_la_convention_restent_lisibles()
    {
        const string legacy = "eval/human/calibration.20260806-9a829dc206d2.jsonl";

        Assert.Equal(
            "eval/human/calibration.20260806-9a829dc206d2.json",
            CalibrationFile.ReportPathFor(legacy).Replace('\\', '/'));
    }
}
