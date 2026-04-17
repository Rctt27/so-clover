namespace SoClover.Domain.Validation;

public enum ClueValidationRule
{
    ExactMatch,
    SimilarStem
}

public sealed record ClueValidationError(
    ClueValidationRule Rule,
    string CardWord,
    Direction? ConflictingDirection);

public sealed record ClueValidationResult(
    bool IsValid,
    IReadOnlyList<ClueValidationError> Errors)
{
    public static ClueValidationResult Valid() =>
        new(true, Array.Empty<ClueValidationError>());

    public static ClueValidationResult Invalid(params ClueValidationError[] errors) =>
        new(false, errors);
}