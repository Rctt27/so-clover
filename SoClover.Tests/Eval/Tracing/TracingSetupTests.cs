using System.Net;
using SoClover.Eval.Cli;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

public class TracingSetupTests
{
    private static Args Parse(params string[] argv) => Args.Parse(["decode", .. argv]);

    [Fact]
    public void Tracing_is_on_by_default_and_off_only_when_written()
    {
        var dev = LangfuseFixtures.Bench("dev").Manifest;
        Assert.True(TracingSetup.WantsTracing(Parse(), dev, isHumanRun: false));
        Assert.False(TracingSetup.WantsTracing(Parse("--trace", "off"), dev, isHumanRun: false));
        Assert.Throws<InvalidOperationException>(() => TracingSetup.WantsTracing(Parse("--trace", "jaeger"), dev, false));
    }

    [Fact]
    public void The_test_bench_and_human_pseudo_runs_refuse_tracing_but_accept_trace_off()
    {
        var test = LangfuseFixtures.Bench("test").Manifest;
        var ex = Assert.Throws<InvalidOperationException>(() => TracingSetup.WantsTracing(Parse(), test, false));
        Assert.Contains("--trace off", ex.Message);
        Assert.False(TracingSetup.WantsTracing(Parse("--trace", "off"), test, false));

        var dev = LangfuseFixtures.Bench("dev").Manifest;
        Assert.Contains("--trace off",
            Assert.Throws<InvalidOperationException>(() => TracingSetup.WantsTracing(Parse(), dev, isHumanRun: true)).Message);
    }

    [Fact]
    public async Task Preflight_checks_the_keys_then_posts_an_empty_otlp_payload()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("{}"));
        await TracePreflight.RunAsync(LangfuseStubHandler.Client(handler), default);

        Assert.Equal("/api/public/projects", handler.Requests[0].PathAndQuery);
        Assert.Equal("/api/public/otel/v1/traces", handler.Requests[1].PathAndQuery);
        Assert.Equal("{\"resourceSpans\":[]}", handler.Requests[1].Body);
    }

    [Fact]
    public async Task Preflight_failure_is_loud_and_suggests_trace_off()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json("{\"message\":\"no\"}", HttpStatusCode.Unauthorized));
        var ex = await Assert.ThrowsAsync<LangfuseException>(() => TracePreflight.RunAsync(LangfuseStubHandler.Client(handler), default));
        Assert.Contains("--trace off", ex.Message);
    }
}
