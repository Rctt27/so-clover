using System.Text;
using SoClover.Domain;
using SoClover.Domain.Validation;

namespace SoClover.Infrastructure.AI.Prompts;

/// <summary>
/// Localized labels injected into the prompt by the C# rendering code. Each supported language
/// supplies its own set so the generated board layout, directions and retry feedback read
/// naturally in that language.
/// </summary>
public sealed record AiCluePromptLabels(
    string CardLineFormat,                  // {0}=position {1}=top {2}=right {3}=bottom {4}=left
    string DirectionLineFormat,             // {0}=direction {1}=wordA {2}=wordB
    string RetryDirectionFormat,            // {0}=direction
    string RetryAttemptFormat,              // {0}=clueText {1}=rejectionReason
    string RejectionRuleWithDirectionFormat, // {0}=rule {1}=cardWord {2}=direction
    string RejectionRuleFormat);            // {0}=rule {1}=cardWord

/// <summary>
/// Language-agnostic So Clover board-clues prompt provider. The only language-specific inputs are
/// the prompt markdown file path, the <see cref="Language"/> tag and the injected
/// <see cref="AiCluePromptLabels"/>.
/// </summary>
public abstract class FileAiCluePromptProvider : IAiCluePromptProvider
{
    private const int MaxFeedbackAttemptsPerDirection = 3;

    // TODO: ce JsonSchemaText fige le schéma v7 (additionalProperties:false) et est désormais
    // obsolète par rapport au prompt v8 (qui demande consideredAlternatives, linkToWord1Explanation,
    // linkToWord2Explanation, linkStrengthWord1, linkStrengthWord2). Aujourd'hui il est exposé via
    // AiCluePromptBundle.JsonSchema mais n'est consommé nulle part (jamais branché sur
    // ChatOptions.ResponseFormat), donc aucun impact runtime. Si on l'active un jour, le synchroniser
    // avec board-clues.md ou retirer "additionalProperties: false" sinon les modèles strict-schema
    // rejetteront les nouveaux champs.
    private const string JsonSchemaText = """
{
  "type": "object",
  "properties": {
    "clues": {
      "type": "array",
      "minItems": 1,
      "maxItems": 4,
      "items": {
        "type": "object",
        "properties": {
          "direction": { "type": "string", "enum": ["Top", "Right", "Bottom", "Left"] },
          "clueWord": { "type": "string", "minLength": 1, "maxLength": 32 },
          "explanation": { "type": "string", "minLength": 1 }
        },
        "required": ["direction", "clueWord", "explanation"],
        "additionalProperties": false
      }
    }
  },
  "required": ["clues"],
  "additionalProperties": false
}
""";

    private static readonly Direction[] AllDirections =
        [Direction.Top, Direction.Right, Direction.Bottom, Direction.Left];

    private readonly FilePromptLoader _loader;
    private readonly string _promptFilePath;
    private readonly AiCluePromptLabels _labels;

    protected FileAiCluePromptProvider(
        FilePromptLoader loader, string promptFilePath, AiCluePromptLabels labels, string language)
    {
        _loader = loader;
        _promptFilePath = promptFilePath;
        _labels = labels;
        Language = language;
    }

    public string Language { get; }

    public AiCluePromptBundle BuildBoardCluesPrompt(BoardCluesPromptContext context)
    {
        ValidateContext(context);

        var sections = _loader.Load(_promptFilePath);
        var cardsByPosition = context.Cards.ToDictionary(c => c.Position);

        var system = sections.System.Trim();
        var user = SubstituteUser(sections.User, sections.RetryFeedback, context, cardsByPosition);

        return new AiCluePromptBundle(system, user, JsonSchemaText, sections.Version);
    }

    public string FormatRejectionReason(ClueValidationResult result)
    {
        return string.Join("; ", result.Errors.Select(e =>
            e.ConflictingDirection is { } d
                ? string.Format(_labels.RejectionRuleWithDirectionFormat, e.Rule, e.CardWord, d)
                : string.Format(_labels.RejectionRuleFormat, e.Rule, e.CardWord)));
    }

    private static void ValidateContext(BoardCluesPromptContext ctx)
    {
        if (ctx.Cards is null || ctx.Cards.Count != 4)
            throw new ArgumentException("Cards must contain exactly 4 entries.", nameof(ctx));

        var positions = ctx.Cards.Select(c => c.Position).ToHashSet();
        if (positions.Count != 4
            || !positions.Contains(BoardPosition.TopLeft)
            || !positions.Contains(BoardPosition.TopRight)
            || !positions.Contains(BoardPosition.BottomRight)
            || !positions.Contains(BoardPosition.BottomLeft))
            throw new ArgumentException("Cards must cover all 4 BoardPositions exactly once.", nameof(ctx));

        if (ctx.RemainingDirections is null || ctx.RemainingDirections.Count == 0)
            throw new ArgumentException("RemainingDirections must be non-empty.", nameof(ctx));

        var remainingSet = ctx.RemainingDirections.ToHashSet();
        foreach (var key in ctx.RejectedPerDirection.Keys)
        {
            if (!remainingSet.Contains(key))
                throw new ArgumentException(
                    $"RejectedPerDirection contains key {key} which is not in RemainingDirections.",
                    nameof(ctx));
        }
    }

    private string SubstituteUser(
        string userTemplate,
        string retryFeedbackTemplate,
        BoardCluesPromptContext context,
        IReadOnlyDictionary<BoardPosition, BoardCardSnapshot> cardsByPosition)
    {
        var boardLayout = BuildBoardLayout(context.Cards);
        var directionsToResolve = BuildDirectionsToResolve(context.RemainingDirections, cardsByPosition);
        var allWordsList = BuildAllBoardWordsList(context.Cards);
        var retryBlock = BuildRetryBlock(retryFeedbackTemplate, context.RejectedPerDirection);

        var sb = new StringBuilder(userTemplate);
        sb.Replace("{{boardLayout}}", boardLayout);
        sb.Replace("{{directionsToResolve}}", directionsToResolve);
        sb.Replace("{{allBoardWordsList}}", allWordsList);
        sb.Replace("{{retryFeedback}}", retryBlock);
        return sb.ToString().Trim();
    }

    private string BuildBoardLayout(IReadOnlyList<BoardCardSnapshot> cards)
    {
        var ordered = new[] { BoardPosition.TopLeft, BoardPosition.TopRight, BoardPosition.BottomRight, BoardPosition.BottomLeft };
        var byPos = cards.ToDictionary(c => c.Position);

        var sb = new StringBuilder();
        foreach (var pos in ordered)
        {
            var c = byPos[pos];
            sb.AppendLine(string.Format(
                _labels.CardLineFormat, pos, c.TopWord, c.RightWord, c.BottomWord, c.LeftWord));
        }
        return sb.ToString().TrimEnd();
    }

    private string BuildDirectionsToResolve(
        IReadOnlyList<Direction> remaining,
        IReadOnlyDictionary<BoardPosition, BoardCardSnapshot> byPos)
    {
        var sb = new StringBuilder();
        foreach (var dir in AllDirections)
        {
            if (!remaining.Contains(dir))
                continue;

            var (cardA, faceA, cardB, faceB) = GetEdgeMapping(dir);
            var wordA = GetOrientedWord(byPos[cardA], faceA);
            var wordB = GetOrientedWord(byPos[cardB], faceB);
            sb.AppendLine(string.Format(_labels.DirectionLineFormat, dir, wordA, wordB));
        }
        return sb.ToString().TrimEnd();
    }

    // Convention "faces extérieures" : chaque clue évoque les deux mots des cartes
    // sur son côté du board, sur les faces visuellement adjacentes au clue
    // (celles qui pointent vers le bord extérieur, donc proches du clue placé sur la bordure).
    private static (BoardPosition CardA, Direction FaceA, BoardPosition CardB, Direction FaceB) GetEdgeMapping(Direction edge)
        => edge switch
        {
            Direction.Top    => (BoardPosition.TopLeft,     Direction.Top,    BoardPosition.TopRight,    Direction.Top),
            Direction.Right  => (BoardPosition.TopRight,    Direction.Right,  BoardPosition.BottomRight, Direction.Right),
            Direction.Bottom => (BoardPosition.BottomRight, Direction.Bottom, BoardPosition.BottomLeft,  Direction.Bottom),
            Direction.Left   => (BoardPosition.BottomLeft,  Direction.Left,   BoardPosition.TopLeft,     Direction.Left),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };

    private static string GetOrientedWord(BoardCardSnapshot card, Direction face)
        => face switch
        {
            Direction.Top    => card.TopWord,
            Direction.Right  => card.RightWord,
            Direction.Bottom => card.BottomWord,
            Direction.Left   => card.LeftWord,
            _ => throw new ArgumentOutOfRangeException(nameof(face)),
        };

    private static string BuildAllBoardWordsList(IReadOnlyList<BoardCardSnapshot> cards)
    {
        var allWords = cards.SelectMany(c =>
            new[] { c.TopWord, c.RightWord, c.BottomWord, c.LeftWord });
        return string.Join("\n", allWords.Select(w => $"- {w}"));
    }

    private string BuildRetryBlock(
        string retryTemplate,
        IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>> rejectedPerDirection)
    {
        if (rejectedPerDirection.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var dir in AllDirections)
        {
            if (!rejectedPerDirection.TryGetValue(dir, out var attempts) || attempts.Count == 0)
                continue;

            var ordered = attempts.Reverse().Take(MaxFeedbackAttemptsPerDirection).ToList();
            sb.AppendLine(string.Format(_labels.RetryDirectionFormat, dir));
            foreach (var a in ordered)
                sb.AppendLine(string.Format(_labels.RetryAttemptFormat, a.ClueText, a.RejectionReason));
            sb.AppendLine();
        }

        return retryTemplate
            .Replace("{{rejectedAttemptsByDirection}}", sb.ToString().TrimEnd())
            .Trim();
    }
}
