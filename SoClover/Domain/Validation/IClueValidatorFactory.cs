namespace SoClover.Domain.Validation;

public interface IClueValidatorFactory
{
    IClueValidator GetFor(string language, bool semanticCheckEnabled);
}