using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace SoClover.Infrastructure.AI.Reasoning;

/// <summary>
/// Injecte le paramètre natif <c>reasoning_effort</c> (OpenAI o-series, gpt-oss, et les modèles
/// LM Studio qui le supportent) via <see cref="ChatOptions.RawRepresentationFactory"/> : l'adaptateur
/// Microsoft.Extensions.AI.OpenAI part de ce <see cref="ChatCompletionOptions"/> de base et superpose
/// par-dessus les propriétés du <see cref="ChatOptions"/> M.E.AI.
/// Note : pour beaucoup de modèles reasoning sous LM Studio l'activation reste pilotée côté serveur
/// (« Enable Thinking ») et reasoning_effort peut être ignoré — c'est inoffensif.
/// </summary>
public sealed class OpenAIReasoningConfigurator : IReasoningRequestConfigurator
{
    private readonly LlmOptions _opts;

    public OpenAIReasoningConfigurator(IOptions<LlmOptions> options)
    {
        _opts = options.Value;
    }

    public void Configure(ChatOptions options)
    {
        var effort = MapEffort(_opts.ReasoningEffort);
        if (effort is null)
            return;

        options.RawRepresentationFactory = _ => new ChatCompletionOptions
        {
            ReasoningEffortLevel = effort,
        };
    }

    private static ChatReasoningEffortLevel? MapEffort(string? value)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "low": return ChatReasoningEffortLevel.Low;
            case "medium": return ChatReasoningEffortLevel.Medium;
            case "high": return ChatReasoningEffortLevel.High;
            default: return null;
        }
    }
}
