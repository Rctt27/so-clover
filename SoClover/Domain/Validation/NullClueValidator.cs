namespace SoClover.Domain.Validation;

public sealed class NullClueValidator : IClueValidator
{
    public static readonly NullClueValidator Instance = new();
    public string Language => "*";
    public ClueValidationResult Validate(string clueText, Direction direction, CloverBoard board)
        => ClueValidationResult.Valid();
}