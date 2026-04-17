namespace SoClover.Domain.Validation;

public sealed class FrenchOffClueValidator : IClueValidator
{
    private const int MinWordLength = 3;

    public string Language => "Français_OFF";

    public ClueValidationResult Validate(string clueText, Direction direction, CloverBoard board)
    {
        var clueNorm = TextNormalizer.Normalize(clueText);
        if (clueNorm.Length == 0)
            return ClueValidationResult.Valid();

        var errors = new List<ClueValidationError>();
        foreach (var (word, wordDirection) in EnumerateBoardWords(board))
        {
            var wordNorm = TextNormalizer.Normalize(word);
            if (wordNorm.Length < MinWordLength)
                continue;

            if (clueNorm.Contains(wordNorm, StringComparison.Ordinal)
                || wordNorm.Contains(clueNorm, StringComparison.Ordinal))
            {
                errors.Add(new ClueValidationError(ClueValidationRule.ExactMatch, word, wordDirection));
            }
        }

        return errors.Count == 0
            ? ClueValidationResult.Valid()
            : ClueValidationResult.Invalid(errors.ToArray());
    }

    private static IEnumerable<(string Word, Direction? Direction)> EnumerateBoardWords(CloverBoard board)
    {
        foreach (var oriented in new[] { board.TopLeft, board.TopRight, board.BottomRight, board.BottomLeft })
        {
            if (oriented == null) continue;
            var card = oriented.Card;
            yield return (card.TopWord, null);
            yield return (card.RightWord, null);
            yield return (card.BottomWord, null);
            yield return (card.LeftWord, null);
        }
    }
}