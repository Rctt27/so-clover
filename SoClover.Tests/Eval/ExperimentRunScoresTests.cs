using System.Net;
using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Le délai de poll de production (3 s) rendrait ces tests lents : ils passent par la surcharge
/// interne à délai injectable (visible via InternalsVisibleTo), production non touchée.
/// </summary>
public class ExperimentRunScoresTests
{
    private static readonly TimeSpan NoDelay = TimeSpan.Zero;

    [Fact]
    public async Task Experiment_absente_puis_presente_est_trouvee_au_second_essai()
    {
        var calls = 0;
        var handler = new LangfuseStubHandler((_, _) =>
        {
            calls++;
            return calls == 1
                ? LangfuseStubHandler.Json("""{"data":[]}""")
                : LangfuseStubHandler.Json("""{"data":[{"id":"exp_1","name":"run.fp"}]}""");
        });

        var datasetRunId = await ExperimentRunScores.WaitForExperimentAsync(
            LangfuseStubHandler.Client(handler), "run.fp",
            DateTime.UnixEpoch, DateTime.UnixEpoch, NoDelay, CancellationToken.None);

        Assert.Equal("exp_1", datasetRunId);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Experiment_jamais_visible_rend_null_apres_les_dix_essais()
    {
        var calls = 0;
        var handler = new LangfuseStubHandler((_, _) =>
        {
            calls++;
            return LangfuseStubHandler.Json("""{"data":[]}""");
        });

        var datasetRunId = await ExperimentRunScores.WaitForExperimentAsync(
            LangfuseStubHandler.Client(handler), "run.fp",
            DateTime.UnixEpoch, DateTime.UnixEpoch, NoDelay, CancellationToken.None);

        Assert.Null(datasetRunId);
        Assert.Equal(10, calls);
    }
}
