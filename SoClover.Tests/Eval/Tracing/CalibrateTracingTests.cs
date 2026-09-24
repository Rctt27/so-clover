using SoClover.Eval.Calibration;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

[Collection(TracingCollection.Name)]
public class CalibrateTracingTests
{
    [Fact]
    public async Task A_clue_is_one_free_trace_in_the_calibration_session_with_its_decodes()
    {
        var bench = LangfuseFixtures.Bench();
        var board = bench.Boards[0];
        var top = board.Directions.Single(d => d.Direction == "Top");
        var fake = new FakeChatClient();
        for (var i = 0; i < 3; i++) fake.Enqueue($$"""{"picked":["{{top.ReferenceWords[0]}}","{{top.ReferenceWords[1]}}"]}""");
        var decoder = new ClueDecoder(EvalLlmConfig.Instrument(fake), new FilePromptLoader(),
            Path.Combine(AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md"), "m", 0.3f, null, 512);

        using var session = TracingTestKit.Start(out var exported);
        var lines = await CalibrateClueUnit.RunAsync(decoder, board, "Top", "Perle", 3,
            new HashSet<(string, string, string, int)>(), "20260919-fp-d3", "fp", 0, bench.Manifest.BenchHash, default);
        session.Checkpoint("Perle");

        Assert.Equal(3, lines.Count);
        var root = TracingTestKit.Single(exported, "calibrate-clue");
        Assert.Equal("20260919-fp-d3", TracingTestKit.Tag(root, EvalTracing.SessionId));
        Assert.Equal("calibration", TracingTestKit.Tag(root, EvalTracing.Environment));
        Assert.Null(TracingTestKit.Tag(root, "langfuse.experiment.id"));
        Assert.Equal(3, exported.Count(a => a.DisplayName.StartsWith("decode-clue") && a.ParentSpanId == root.SpanId));
    }
}
