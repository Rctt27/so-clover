using SoClover.Domain.Validation;

namespace SoClover.Infrastructure.Validation;

public sealed class ClueValidatorFactory : IClueValidatorFactory
{
    public IClueValidator GetFor(string language, bool semanticCheckEnabled)
    {
        if (!semanticCheckEnabled)
            return NullClueValidator.Instance;

        // Language routing lives in SemanticValidationSupport so this factory and Game.SemanticClueCheckEnabled
        // can never disagree about which dictionaries are covered.
        return SemanticValidationSupport.Find(language) ?? NullClueValidator.Instance;
    }
}
