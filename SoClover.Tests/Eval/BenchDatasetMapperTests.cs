using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class BenchDatasetMapperTests
{
    [Fact]
    public void Un_item_par_direction_avec_un_id_stable()
    {
        var items = BenchDatasetMapper.ToItems(LangfuseFixtures.Bench());

        Assert.Equal(
            new[] { "dev-001-Top", "dev-001-Right", "dev-001-Bottom", "dev-001-Left" },
            items.Select(i => i.Id));
    }

    [Fact]
    public void L_entree_porte_les_quatre_cartes_positionnees_et_les_seize_mots_la_sortie_la_paire()
    {
        var bench = LangfuseFixtures.Bench();
        var top = BenchDatasetMapper.ToItems(bench)[0];

        var cards = top.Input["cards"]!.AsArray();
        Assert.Equal(4, cards.Count);
        Assert.Equal("TopLeft", (string?)cards[0]!["position"]);
        Assert.Equal("Paradis", (string?)cards[0]!["top"]);
        Assert.Equal(16, top.Input["words"]!.AsArray().Count);
        Assert.Equal("Top", (string?)top.Input["direction"]);
        Assert.Equal(
            bench.Boards[0].Directions[0].ReferenceWords,
            top.ExpectedOutput["referenceWords"]!.AsArray().Select(w => (string)w!));
        Assert.Equal("416b819a41a1", (string?)top.Metadata["benchHash"]);
    }

    [Fact]
    public void Le_nom_du_dataset_derive_du_benchId()
    {
        Assert.Equal("soclover-bench-dev", BenchDatasetMapper.DatasetName(LangfuseFixtures.Bench().Manifest));
    }

    [Fact]
    public void Le_banc_de_test_est_refuse()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => BenchDatasetMapper.RequireNotTestBench(LangfuseFixtures.Bench("test").Manifest));

        Assert.Contains("jalon", ex.Message);
    }
}
