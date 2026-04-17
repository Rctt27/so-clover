namespace SoClover.Domain.Validation;

public interface IClueValidator
{
    string Language { get; }
    ClueValidationResult Validate(string clueText, Direction direction, CloverBoard board);
}