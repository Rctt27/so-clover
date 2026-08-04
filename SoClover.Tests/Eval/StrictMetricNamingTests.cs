using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Même geste que <c>board_solved</c> → <c>board_solved_first_try</c> : le nom porte la
/// contrainte. <c>strict_2of2</c> n'exige pas « les 2 mots sur 2 » mais l'unanimité des trois
/// décodages ; le nom seul laissait croire à un taux de succès.
/// <para>
/// La clé sérialisée <c>strict2Of2</c> ne bouge pas : elle figure dans tous les
/// <c>.metrics.json</c> déjà produits, que <c>calibrate</c> relit.
/// </para>
/// </summary>
public class StrictMetricNamingTests
{
    [Fact]
    public void L_en_tete_du_registre_porte_la_contrainte_d_unanimite()
    {
        Assert.Contains("strict_2of2_all_decodes", LedgerWriter.HeaderRow);
    }

    [Fact]
    public void L_en_tete_conserve_ses_dix_huit_colonnes()
    {
        var columns = LedgerWriter.HeaderRow.Trim('|').Split('|');

        Assert.Equal(18, columns.Length);
    }

    // Le contrat de relecture : renommer la propriété C# changerait la clé JSON et casserait
    // les .metrics.json de l'historique.
    [Fact]
    public void La_cle_serialisee_reste_strict2Of2()
    {
        var json = EvalJson.Serialize(new MetricsReport(
            "run", "eval/boards.dev.jsonl", "a1b2c3d4e5f6", 1, 4,
            1.0, 1.0, 0.0, 0.3, 0.25, 0.5, 0.0, 0.0, [], 0.0, 4, 4,
            new Dictionary<(string, string), double>(), MetricCounts.Zero));

        Assert.Contains("\"strict2Of2\"", json);
    }
}
