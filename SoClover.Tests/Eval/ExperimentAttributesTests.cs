using System.Linq;
using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ExperimentAttributesTests
{
    [Fact]
    public void Common_attributes_name_the_experiment_the_item_and_its_root()
    {
        var attributes = ExperimentAttributes.Common(
            "run.fp", "ds-1", "fp", LangfuseFixtures.RunManifest(), LangfuseFixtures.DecodeManifest(), "dev-001-Top", "abcd")
            .ToDictionary(a => a.Key, a => a.Value);

        Assert.Equal("run.fp", attributes["langfuse.experiment.id"]);
        Assert.Equal("ds-1", attributes["langfuse.experiment.dataset.id"]);
        Assert.Equal("dev-001-Top", attributes["langfuse.experiment.item.id"]);
        Assert.Equal("abcd", attributes["langfuse.experiment.item.root_observation_id"]);
        Assert.Equal("fp", attributes["langfuse.experiment.metadata.decoder_fingerprint"]);
    }

    [Fact]
    public void The_search_window_covers_the_run_and_the_decode_dates()
    {
        var run = new System.DateTime(2026, 7, 28, 10, 0, 0, System.DateTimeKind.Utc);
        var decode = new System.DateTime(2026, 9, 19, 10, 0, 0, System.DateTimeKind.Utc);
        var (from, to) = LangfuseExportCommand.SearchWindow(run, decode);
        Assert.Equal(run.AddDays(-1), from);
        Assert.Equal(decode.AddDays(1), to);
    }
}
