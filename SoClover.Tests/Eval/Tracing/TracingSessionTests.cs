using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

[Collection(TracingCollection.Name)]
public class TracingSessionTests
{
    [Fact]
    public void A_root_started_with_a_deterministic_trace_id_is_exported_without_parent()
    {
        var traceId = OtlpIds.TraceId("run-x", "dev-001-Top");
        using (var session = TracingTestKit.Start(out var exported))
        {
            using (var root = EvalTracing.StartRoot("generate-direction", traceId))
                Assert.NotNull(root);
            session.Checkpoint("dev-001-Top");

            var span = TracingTestKit.Single(exported, "generate-direction");
            Assert.Equal(traceId, span.TraceId.ToHexString());
            Assert.Equal(default, span.ParentSpanId);
        }
    }

    [Fact]
    public void Checkpoint_throws_when_an_export_failed_and_names_the_unit()
    {
        using var session = TracingTestKit.StartFailing();
        using (EvalTracing.StartRoot("generate-direction", OtlpIds.TraceId("run-x", "dev-001-Top"))) { }

        var ex = Assert.Throws<TracingLostException>(() => session.Checkpoint("dev-001-Top"));
        Assert.Contains("dev-001-Top", ex.Message);
        Assert.Contains("reprendre avec la même commande", ex.Message);
    }

    [Fact]
    public void Off_session_starts_no_activity_and_never_throws()
    {
        using var session = TracingSession.Off();
        Assert.False(session.IsOn);
        Assert.Null(EvalTracing.StartRoot("generate-direction", OtlpIds.TraceId("run-x", "dev-001-Top")));
        session.Checkpoint("dev-001-Top");
        Assert.Equal(TracingManifest.Off, session.Manifest);
    }

    [Fact]
    public void Resume_is_refused_when_the_tracing_mode_differs()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TracingManifest.RequireSameMode(TracingManifest.Off, TracingManifest.Langfuse("http://h"), "f.jsonl"));
        Assert.Contains("--trace off", ex.Message);

        // Manifeste historique (tracing absent) : reprenable seulement sans traçage.
        TracingManifest.RequireSameMode(null, TracingManifest.Off, "f.jsonl");
        Assert.Throws<InvalidOperationException>(() =>
            TracingManifest.RequireSameMode(null, TracingManifest.Langfuse("http://h"), "f.jsonl"));
        TracingManifest.RequireSameMode(TracingManifest.Langfuse("http://h"), TracingManifest.Langfuse("http://h"), "f.jsonl");
    }

    [Fact]
    public void Tracing_manifest_serializes_as_target_and_host()
    {
        Assert.Equal("""{"target":"langfuse","host":"http://h"}""", EvalJson.Serialize(TracingManifest.Langfuse("http://h")));
        Assert.Equal("""{"target":"off"}""", EvalJson.Serialize(TracingManifest.Off));
    }
}
