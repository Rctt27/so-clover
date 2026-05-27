using Anthropic.SDK.Messaging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace SoClover.Infrastructure.AI.Reasoning;

/// <summary>
/// Active l'extended thinking d'Anthropic via <see cref="ChatOptions.RawRepresentationFactory"/> :
/// l'IChatClient d'Anthropic.SDK part de ce <see cref="MessageParameters"/> de base et y superpose les
/// propriétés du <see cref="ChatOptions"/> M.E.AI. Le budget vient de
/// <see cref="LlmOptions.ThinkingBudgetTokens"/> ; sans budget explicite, on n'active rien.
/// </summary>
public sealed class AnthropicReasoningConfigurator : IReasoningRequestConfigurator
{
    private readonly LlmOptions _opts;

    public AnthropicReasoningConfigurator(IOptions<LlmOptions> options)
    {
        _opts = options.Value;
    }

    public void Configure(ChatOptions options)
    {
        if (_opts.ThinkingBudgetTokens is not { } budget || budget <= 0)
            return;

        options.RawRepresentationFactory = _ => new MessageParameters
        {
            Thinking = new ThinkingParameters
            {
                BudgetTokens = budget,
            },
        };
    }
}
