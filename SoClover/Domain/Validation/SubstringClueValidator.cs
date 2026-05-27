namespace SoClover.Domain.Validation;

/// <summary>
/// Base validator implementing R1 — bidirectional substring matching between the clue and every board
/// word (after normalization, min length). R1 is language-agnostic. Subclasses may add language-specific
/// rules by overriding <see cref="CheckAdditionalRules"/>; R1 always wins for a given word (no additional
/// rule runs once R1 fired on that word).
/// </summary>
public abstract class SubstringClueValidator : IClueValidator
{
    protected const int MinWordLength = 3;

    public abstract string Language { get; }

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
                || (clueNorm.Length >= MinWordLength && wordNorm.Contains(clueNorm, StringComparison.Ordinal)))
            {
                errors.Add(new ClueValidationError(ClueValidationRule.ExactMatch, word, wordDirection));
                continue; // R1 wins — skip additional rules for this word
            }

            CheckAdditionalRules(clueNorm, wordNorm, word, wordDirection, errors);
        }

        return errors.Count == 0
            ? ClueValidationResult.Valid()
            : ClueValidationResult.Invalid(errors.ToArray());
    }

    /// <summary>
    /// Language-specific rules applied to a board word that R1 did not already reject. No-op by default.
    /// </summary>
    protected virtual void CheckAdditionalRules(
        string clueNorm,
        string wordNorm,
        string word,
        Direction? wordDirection,
        List<ClueValidationError> errors)
    {
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
