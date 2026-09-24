using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Runner;
using SoClover.Eval.Tracing;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

[Collection(TracingCollection.Name)]
public class GenerateTracingTests
{
    private static BenchBoard Board() =>
        BenchGenerator.Generate(
            "dev", 20260726001, 1,
            ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
             "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont"],
            "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)).Boards[0];

    private static ClueRunner Runner(FakeChatClient chat, int maxAttempts) =>
        new(new AiClueLlmCaller(SoClover.Eval.Config.EvalLlmConfig.Instrument(chat),
                Options.Create(new LlmOptions { DefaultModel = "modele-test", DefaultTemperature = 1.0 })),
            new FrenchAiCluePromptProvider(), new FrenchOffClueValidator(), maxAttempts, "Français_OFF");

    private static string Json(string clue) =>
        $$"""{"direction":"Top","clueWord":"{{clue}}","explanation":"e"}""";

    [Fact]
    public async Task A_direction_is_one_trace_with_one_span_per_attempt_and_one_llm_call_each()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json("Forêt"));    // mot du board : rejeté
        chat.Enqueue(Json("Hôpital"));  // valide
        using var session = TracingTestKit.Start(out var exported);

        var attempts = await GenerateDirectionUnit.RunAsync(Runner(chat, 3), Board(), Direction.Top, 3, "run-x", default);
        session.Checkpoint("dev-001-Top");

        Assert.Equal(2, attempts.Count);
        var root = TracingTestKit.Single(exported, "generate-direction");
        Assert.Equal(OtlpIds.TraceId("run-x", "dev-001-Top"), root.TraceId.ToHexString());
        Assert.Equal("run-x", TracingTestKit.Tag(root, EvalTracing.SessionId));
        Assert.Equal("generate", TracingTestKit.Tag(root, EvalTracing.Environment));
        Assert.Contains("Hôpital", TracingTestKit.Tag(root, EvalTracing.Output));

        var spans = exported.Where(a => a.DisplayName.StartsWith("generate attempt")).ToList();
        Assert.Equal(2, spans.Count);
        Assert.Equal("false", TracingTestKit.Tag(spans[0], EvalTracing.Metadata("valid")));
        Assert.Equal("true", TracingTestKit.Tag(spans[1], EvalTracing.Metadata("valid")));
        Assert.All(spans, s => Assert.Single(exported, c => c.Source.Name == EvalTracing.ChatSourceName && c.ParentSpanId == s.SpanId));
    }

    [Fact]
    public async Task Without_tracing_the_unit_returns_the_same_attempts()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json("Hôpital"));
        using var session = TracingSession.Off();

        var attempts = await GenerateDirectionUnit.RunAsync(Runner(chat, 3), Board(), Direction.Top, 3, "run-x", default);

        Assert.Single(attempts);
        Assert.True(attempts[0].Valid);
    }

    [Fact]
    public async Task A_failed_export_stops_before_anything_is_written()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json("Hôpital"));
        using var session = TracingTestKit.StartFailing();
        var path = Path.Combine(Path.GetTempPath(), $"run-{Guid.NewGuid():N}.jsonl");
        RunFile.WriteManifest(path, LangfuseFixtures.RunManifest());

        var attempts = await GenerateDirectionUnit.RunAsync(Runner(chat, 3), Board(), Direction.Top, 3, "run-x", default);
        // Même séquence que GenerateCommand : checkpoint, PUIS écriture.
        Assert.Throws<TracingLostException>(() =>
        {
            session.Checkpoint("dev-001 Top");
            foreach (var a in attempts) RunFile.AppendAttempt(path, a);
        });

        Assert.Single(attempts);
        Assert.Empty(RunFile.Read(path).Attempts);
    }
}
