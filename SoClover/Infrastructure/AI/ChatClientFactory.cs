using System.ClientModel;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Builds the application's IChatClient by selecting a provider (OpenAI-compatible or
/// Anthropic) according to <see cref="LlmOptions.Provider"/>, then composing the
/// decorator pipeline <c>Timeout(Throttle(Provider))</c>.
/// The semaphore inside ThrottlingChatClient is process-wide, so this factory MUST be
/// called once at startup and the resulting IChatClient registered as a singleton.
/// </summary>
public sealed class ChatClientFactory
{
    private readonly LlmOptions _opts;

    public ChatClientFactory(IOptions<LlmOptions> options)
    {
        _opts = options.Value;
    }

    public IChatClient Create()
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            throw new InvalidOperationException(
                $"LlmOptions.ApiKey must be a non-empty value for provider {_opts.Provider}.");
        }

        IChatClient providerClient = _opts.Provider switch
        {
            LlmProvider.OpenAI => CreateOpenAICompatibleClient(),
            LlmProvider.Anthropic => CreateAnthropicClient(),
            _ => throw new InvalidOperationException(
                $"Unknown LlmProvider value: {_opts.Provider}.")
        };

        var throttled = new ThrottlingChatClient(providerClient, _opts.MaxConcurrency);
        var timed = new TimeoutChatClient(throttled, TimeSpan.FromSeconds(_opts.TimeoutSeconds));
        return timed;
    }

    private IChatClient CreateOpenAICompatibleClient()
    {
        // OpenAI client (also drives LM Studio) — endpoint is overridden via OpenAIClientOptions.
        var openAiOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_opts.BaseUrl)
        };
        var openAi = new OpenAIClient(new ApiKeyCredential(_opts.ApiKey), openAiOptions);
        return openAi.GetChatClient(_opts.DefaultModel).AsIChatClient();
    }

    private IChatClient CreateAnthropicClient()
    {
        // Anthropic.SDK exposes IChatClient via AnthropicClient.Messages since v5.
        var anthropic = new AnthropicClient(_opts.ApiKey);
        return anthropic.Messages;
    }
}