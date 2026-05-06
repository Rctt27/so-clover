using System.Text;
using SoClover.Domain;

namespace SoClover.Infrastructure.AI.Prompts;

public sealed class FrenchAiCluePromptProvider : IAiCluePromptProvider
{
    private const int MaxFeedbackAttemptsPerDirection = 3;
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

    public FrenchAiCluePromptProvider()
        : this(new FilePromptLoader(), DefaultPromptPath()) { }

    internal FrenchAiCluePromptProvider(FilePromptLoader loader, string promptFilePath)
    {
        _loader = loader;
        _promptFilePath = promptFilePath;
    }

    public string Language => "Français_OFF";

    public AiCluePromptBundle BuildBoardCluesPrompt(BoardCluesPromptContext context)
    {
        ValidateContext(context);

        var sections = _loader.Load(_promptFilePath);
        var cardsByPosition = context.Cards.ToDictionary(c => c.Position);

        var system = sections.System.Trim();
        var user = SubstituteUser(sections.User, sections.RetryFeedback, context, cardsByPosition);

        return new AiCluePromptBundle(system, user, JsonSchemaText);
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

    private static string SubstituteUser(
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

    private static string BuildBoardLayout(IReadOnlyList<BoardCardSnapshot> cards)
    {
        var ordered = new[] { BoardPosition.TopLeft, BoardPosition.TopRight, BoardPosition.BottomRight, BoardPosition.BottomLeft };
        var byPos = cards.ToDictionary(c => c.Position);

        var sb = new StringBuilder();
        foreach (var pos in ordered)
        {
            var c = byPos[pos];
            sb.AppendLine(
                $"- Carte {pos} : Top=\"{c.TopWord}\" Right=\"{c.RightWord}\" Bottom=\"{c.BottomWord}\" Left=\"{c.LeftWord}\"");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildDirectionsToResolve(
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
            sb.AppendLine(
                $"- {dir} → mots \"{wordA}\" (carte {cardA}, face {faceA}) + \"{wordB}\" (carte {cardB}, face {faceB})");
        }
        return sb.ToString().TrimEnd();
    }

    private static (BoardPosition CardA, Direction FaceA, BoardPosition CardB, Direction FaceB) GetEdgeMapping(Direction edge)
        => edge switch
        {
            Direction.Top    => (BoardPosition.TopLeft,     Direction.Bottom, BoardPosition.TopRight,    Direction.Bottom),
            Direction.Right  => (BoardPosition.TopRight,    Direction.Left,   BoardPosition.BottomRight, Direction.Left),
            Direction.Bottom => (BoardPosition.BottomRight, Direction.Top,    BoardPosition.BottomLeft,  Direction.Top),
            Direction.Left   => (BoardPosition.BottomLeft,  Direction.Right,  BoardPosition.TopLeft,     Direction.Right),
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

    private static string BuildRetryBlock(
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
            sb.AppendLine($"Direction {dir} :");
            foreach (var a in ordered)
                sb.AppendLine($"  - \"{a.ClueText}\" rejeté ({a.RejectionReason})");
            sb.AppendLine();
        }

        return retryTemplate
            .Replace("{{rejectedAttemptsByDirection}}", sb.ToString().TrimEnd())
            .Trim();
    }

    private static string DefaultPromptPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Infrastructure", "AI", "Prompts", "fr", "board-clues.md");
    }
}
