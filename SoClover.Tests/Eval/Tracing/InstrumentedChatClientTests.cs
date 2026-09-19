using Microsoft.Extensions.AI;
using SoClover.Eval.Config;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;
using SoClover.Tests.AI;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

[Collection(TracingCollection.Name)]
public class InstrumentedChatClientTests
{
    [Fact]
    public async Task Each_llm_call_is_a_child_span_carrying_the_rendered_prompt_and_the_raw_response()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("réponse-brute-test");
        using var chat = EvalLlmConfig.Instrument(fake);

        using var session = TracingTestKit.Start(out var exported);
        string rootSpanId;
        using (var root = EvalTracing.StartRoot("decode-clue #0", OtlpIds.TraceId("exp", "dev-001-Top")))
        {
            rootSpanId = root!.SpanId.ToHexString();
            await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "prompt-rendu-test")]);
        }
        session.Checkpoint("dev-001-Top");

        var call = exported.Single(a => a.Source.Name == EvalTracing.ChatSourceName);
        Assert.Equal(rootSpanId, call.ParentSpanId.ToHexString());

        // Langfuse ne lit ni les attributs gen_ai ni les ActivityEvents (spike, hypothèse O1) :
        // il lit langfuse.observation.input/output posés en tags sur le span chat lui-même. On
        // n'assert donc PAS le dump attributs+events (déjà vrai sans le repli O1) mais les deux
        // tags directement.
        Assert.Contains("prompt-rendu-test", TracingTestKit.Tag(call, EvalTracing.Input));
        Assert.Equal("réponse-brute-test", TracingTestKit.Tag(call, EvalTracing.Output));
    }

    [Fact]
    public async Task Without_a_session_the_instrumented_client_answers_normally()
    {
        var fake = new FakeChatClient();
        fake.Enqueue("ok");
        using var chat = EvalLlmConfig.Instrument(fake);
        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "x")]);
        Assert.Equal("ok", response.Text);
    }
}
