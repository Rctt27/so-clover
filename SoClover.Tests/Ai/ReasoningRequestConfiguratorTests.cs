using Anthropic.SDK.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Reasoning;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class ReasoningRequestConfiguratorTests
{
    private static IOptions<LlmOptions> Opts(LlmOptions o) => Options.Create(o);

    [Fact]
    public void Null_configurator_leaves_options_untouched()
    {
        var options = new ChatOptions();

        new NullReasoningConfigurator().Configure(options);

        Assert.Null(options.RawRepresentationFactory);
    }

    [Fact]
    public void OpenAI_configurator_sets_reasoning_effort_when_specified()
    {
        var options = new ChatOptions();
        var configurator = new OpenAIReasoningConfigurator(
            Opts(new LlmOptions { ReasoningEffort = "high" }));

        configurator.Configure(options);

        Assert.NotNull(options.RawRepresentationFactory);
        var raw = Assert.IsType<ChatCompletionOptions>(options.RawRepresentationFactory!(null!));
        Assert.Equal(ChatReasoningEffortLevel.High, raw.ReasoningEffortLevel);
    }

    [Fact]
    public void OpenAI_configurator_is_noop_when_effort_is_null()
    {
        var options = new ChatOptions();
        var configurator = new OpenAIReasoningConfigurator(
            Opts(new LlmOptions { ReasoningEffort = null }));

        configurator.Configure(options);

        Assert.Null(options.RawRepresentationFactory);
    }

    [Fact]
    public void Anthropic_configurator_sets_thinking_budget_when_specified()
    {
        var options = new ChatOptions();
        var configurator = new AnthropicReasoningConfigurator(
            Opts(new LlmOptions { ThinkingBudgetTokens = 2048 }));

        configurator.Configure(options);

        Assert.NotNull(options.RawRepresentationFactory);
        var raw = Assert.IsType<MessageParameters>(options.RawRepresentationFactory!(null!));
        Assert.NotNull(raw.Thinking);
        Assert.Equal(2048, raw.Thinking!.BudgetTokens);
    }

    [Fact]
    public void Anthropic_configurator_is_noop_when_budget_is_null_or_nonpositive()
    {
        var options = new ChatOptions();
        var configurator = new AnthropicReasoningConfigurator(
            Opts(new LlmOptions { ThinkingBudgetTokens = null }));

        configurator.Configure(options);

        Assert.Null(options.RawRepresentationFactory);
    }
}
