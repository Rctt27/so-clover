namespace SoClover.Domain.Validation;

public sealed class FrenchOffClueValidator : IClueValidator
{
    private const int MinWordLength = 3;

    public string Language => "Français_OFF";
    private static readonly HashSet<char> Voyelles = new() { 'a', 'e', 'i', 'o', 'u', 'y' };

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

            // R1 — bidirectional substring on full word
            if (clueNorm.Contains(wordNorm, StringComparison.Ordinal)
                || wordNorm.Contains(clueNorm, StringComparison.Ordinal))
            {
                errors.Add(new ClueValidationError(ClueValidationRule.ExactMatch, word, wordDirection));
                continue; // R1 wins — skip R2 for this word
            }

            // R2 — voyelle stem
            if (!Voyelles.Contains(wordNorm[^1]))
                continue;

            var stem = wordNorm[..^1];
            if (stem.Length < MinWordLength)
                continue;

            if (clueNorm.Contains(stem, StringComparison.Ordinal)
                || stem.Contains(clueNorm, StringComparison.Ordinal))
            {
                errors.Add(new ClueValidationError(ClueValidationRule.SimilarStem, word, wordDirection));
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