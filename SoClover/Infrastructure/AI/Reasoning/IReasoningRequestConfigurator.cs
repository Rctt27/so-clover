using Microsoft.Extensions.AI;

namespace SoClover.Infrastructure.AI.Reasoning;

/// <summary>
/// Applique, sur un <see cref="ChatOptions"/> déjà construit, les paramètres de raisonnement natifs
/// spécifiques au provider courant (ex. reasoning_effort côté OpenAI/LM Studio, thinking budget côté
/// Anthropic). L'activation et les valeurs viennent de <see cref="LlmOptions"/>.
/// Le choix de l'implémentation se fait au câblage DII selon <see cref="LlmProvider"/> et
/// <see cref="LlmOptions.ReasoningEnabled"/> — le UseCase reste agnostique.
/// </summary>
public interface IReasoningRequestConfigurator
{
    void Configure(ChatOptions options);
}

/// <summary>
/// No-op : utilisé quand le mode reasoning est désactivé, ou pour un provider sans paramètre natif.
/// </summary>
public sealed class NullReasoningConfigurator : IReasoningRequestConfigurator
{
    public void Configure(ChatOptions options)
    {
        // Volontairement vide : aucun paramètre natif injecté.
    }
}
