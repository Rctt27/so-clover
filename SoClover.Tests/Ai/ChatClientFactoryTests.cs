using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class ChatClientFactoryTests
{
    private static IOptions<LlmOptions> Options(LlmOptions opts) =>
        new OptionsWrapper<LlmOptions>(opts);

    [Fact]
    public void OpenAI_provider_returns_a_non_null_chat_client()
    {
        var factory = new ChatClientFactory(Options(new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            BaseUrl = "http://localhost:1234/v1",
            ApiKey = "lm-studio",
            DefaultModel = "test-model",
            MaxConcurrency = 4,
            TimeoutSeconds = 30
        }));

        using var client = factory.Create();

        Assert.NotNull(client);
    }

    [Fact]
    public void Anthropic_provider_returns_a_non_null_chat_client()
    {
        var factory = new ChatClientFactory(Options(new LlmOptions
        {
            Provider = LlmProvider.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = "sk-ant-test",
            DefaultModel = "claude-sonnet-4-6",
            MaxConcurrency = 4,
            TimeoutSeconds = 30
        }));

        using var client = factory.Create();

        Assert.NotNull(client);
    }

    [Fact]
    public void Decorator_pipeline_is_Timeout_wrapping_Throttle_wrapping_provider()
    {
        var factory = new ChatClientFactory(Options(new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            BaseUrl = "http://localhost:1234/v1",
            ApiKey = "lm-studio",
            DefaultModel = "test",
            MaxConcurrency = 2,
            TimeoutSeconds = 5
        }));

        using var client = factory.Create();

        // Outermost layer must be the timeout decorator.
        var timeout = Assert.IsType<TimeoutChatClient>(client);
        Assert.Equal(TimeSpan.FromSeconds(5), timeout.Timeout);

        // Next layer is the throttling decorator.
        var inner = (IChatClient?)typeof(DelegatingChatClient)
            .GetProperty("InnerClient", System.Reflection.BindingFlags.Instance
                                       | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(timeout);

        var throttle = Assert.IsType<ThrottlingChatClient>(inner);
        Assert.Equal(2, throttle.MaxConcurrency);
    }

    [Fact]
    public void Empty_ApiKey_for_Anthropic_throws_during_creation()
    {
        var factory = new ChatClientFactory(Options(new LlmOptions
        {
            Provider = LlmProvider.Anthropic,
            BaseUrl = "https://api.anthropic.com",
            ApiKey = "",
            DefaultModel = "claude-sonnet-4-6",
            MaxConcurrency = 4,
            TimeoutSeconds = 30
        }));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Create());
        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    public void Unknown_provider_value_throws()
    {
        // Cast a numeric value not defined in the enum to force the default branch.
        var factory = new ChatClientFactory(Options(new LlmOptions
        {
            Provider = (LlmProvider)999,
            BaseUrl = "http://localhost:1234/v1",
            ApiKey = "x",
            DefaultModel = "x",
            MaxConcurrency = 4,
            TimeoutSeconds = 30
        }));

        Assert.Throws<InvalidOperationException>(() => factory.Create());
    }
}