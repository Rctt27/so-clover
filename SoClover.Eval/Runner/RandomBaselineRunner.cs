using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;

namespace SoClover.Eval.Runner;

/// <summary>
/// Plancher aléatoire : les « indices » sont tirés du dictionnaire et <b>filtrés par
/// <see cref="ClueAcceptance"/></b> pour rester des indices <i>légaux</i> — sinon on mesurerait
/// le validateur, pas le décodeur. Le run produit se décode et se score comme n'importe quel autre.
/// <para>
/// Porte associée : <c>recovery ≤ 0,15</c>. Au-delà, le décodeur devine à partir de rien et son
/// prompt est à revoir avant toute autre mesure.
/// </para>
/// </summary>
public sealed class RandomBaselineRunner
{
    /// <summary>Au-delà, on abandonne plutôt que de boucler sur un dictionnaire trop contraint.</summary>
    public const int MaxDraws = 200;

    private readonly IReadOnlyList<string> _dictionary;
    private readonly IClueValidator _validator;
    private readonly Xoshiro256SS _rng;

    public RandomBaselineRunner(IReadOnlyList<string> dictionary, IClueValidator validator, long seed)
    {
        if (dictionary.Count == 0)
            throw new ArgumentException("Le dictionnaire ne peut pas être vide.", nameof(dictionary));

        _dictionary = dictionary;
        _validator = validator;
        _rng = new Xoshiro256SS(seed);
    }

    public RunAttempt RunDirection(BenchBoard board, Direction direction)
    {
        var clover = BenchBoardMapper.ToCloverBoard(board);

        for (var draw = 0; draw < MaxDraws; draw++)
        {
            var candidate = _dictionary[_rng.NextInt(_dictionary.Count)];

            ClueValidationResult result;
            try
            {
                result = ClueAcceptance.Check(candidate, direction, clover, _validator);
            }
            catch (InvalidClueException)
            {
                continue;
            }

            if (!result.IsValid)
                continue;

            return new RunAttempt(
                Kind: "attempt",
                BoardId: board.BoardId,
                Direction: direction.ToString(),
                Attempt: 0,
                Clue: candidate,
                Candidates: null,
                Explanation: "random-baseline",
                Valid: true,
                RejectionRules: [],
                FailureKind: null,
                LatencyMs: 0,
                InputTokens: null,
                OutputTokens: null,
                PromptVersion: null,
                EffectiveModel: "random-baseline");
        }

        return new RunAttempt(
            Kind: "attempt",
            BoardId: board.BoardId,
            Direction: direction.ToString(),
            Attempt: 0,
            Clue: null,
            Candidates: null,
            Explanation: "random-baseline",
            Valid: false,
            RejectionRules: [],
            FailureKind: "randomBaselineExhausted",
            LatencyMs: 0,
            InputTokens: null,
            OutputTokens: null,
            PromptVersion: null,
            EffectiveModel: "random-baseline");
    }
}
