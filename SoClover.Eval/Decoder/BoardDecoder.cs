using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Decoder;

/// <summary>
/// Décodeur board complet (N3). Reçoit les 4 indices <b>étiquetés par direction</b> — le joueur
/// humain voit bien quel indice borde quelle arête — et les 16 mots mélangés, et rend une
/// affectation complète : 2 mots par direction.
/// <para>
/// <b>Choix de modélisation assumé</b> : le vrai jeu fait placer des <i>cartes</i> avec la bonne
/// rotation, ce qui engage aussi les 8 faces intérieures. N3 mesure l'affectation mot → arête,
/// donc une <b>borne supérieure optimiste</b> du score réel. C'est suffisant pour son usage :
/// détecter les collisions inter-directions (<c>recovery</c> bon + <c>board_solved</c> mauvais
/// = mode M6).
/// </para>
/// </summary>
public sealed class BoardDecoder
{
    private const int MaxFormatAttempts = 2;
    private const int SlotsPerBoard = 8;

    private readonly IChatClient _chatClient;
    private readonly ParsedPromptSections _sections;
    private readonly ChatOptions _chatOptions;

    public BoardDecoder(
        IChatClient chatClient,
        FilePromptLoader loader,
        string promptPath,
        string modelId,
        float temperature,
        float? topP,
        int? maxOutputTokens)
    {
        _chatClient = chatClient;
        _sections = loader.Load(promptPath);
        _chatOptions = new ChatOptions { ModelId = modelId, Temperature = temperature };
        if (topP is { } p) _chatOptions.TopP = p;
        if (maxOutputTokens is { } m) _chatOptions.MaxOutputTokens = m;
    }

    public int? PromptVersion => _sections.Version;

    public async Task<BoardDecodeLine> DecodeAsync(
        BenchBoard board,
        IReadOnlyDictionary<Direction, string> cluesByDirection,
        string benchHash,
        CancellationToken ct)
    {
        var seed = ShuffleSeed.ForBoard(benchHash, board.BoardId);
        var presented = ShuffleSeed.Shuffle(BenchBoardMapper.AllWords(board), seed);
        var byNormalized = presented.ToDictionary(TextNormalizer.Normalize, w => w);

        var clueBlock = string.Join("\n", BoardGeometry.AllDirections
            .Where(cluesByDirection.ContainsKey)
            .Select(d => $"- {d} : {cluesByDirection[d]}"));

        var userPrompt = new StringBuilder(_sections.User)
            .Replace("{{shuffledBoardWords}}", string.Join("\n", presented.Select(w => $"- {w}")))
            .Replace("{{cluesByDirection}}", clueBlock)
            .ToString()
            .Trim();

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, _sections.System.Trim()),
            new ChatMessage(ChatRole.User, userPrompt),
        };

        var stopwatch = Stopwatch.StartNew();
        string? lastFailure = null;

        for (var attempt = 0; attempt < MaxFormatAttempts; attempt++)
        {
            ChatResponse response;
            try
            {
                response = await _chatClient
                    .GetResponseAsync(messages, options: _chatOptions, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Failure(board, seed, "timeout", stopwatch);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                Console.Error.WriteLine($"AVERTISSEMENT : décodage board — erreur de transport ({ex.Message}).");
                return Failure(board, seed, "transport", stopwatch);
            }

            var text = response.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                lastFailure = "empty";
                continue;
            }

            var raw = TryParseAssignment(
                ClueDecoder.StripFences(ClueDecoder.StripThinkTags(text)));
            if (raw is null)
            {
                lastFailure = "unparseable";
                continue;
            }

            var assignment = new Dictionary<string, IReadOnlyList<string>>();
            var outOfVocabulary = false;

            foreach (var direction in BoardGeometry.AllDirections)
            {
                var words = raw[direction.ToString()];
                var canonical = new List<string>();
                foreach (var word in words)
                {
                    if (byNormalized.TryGetValue(TextNormalizer.Normalize(word), out var boardWord))
                        canonical.Add(boardWord);
                    else
                        outOfVocabulary = true;
                }
                assignment[direction.ToString()] = canonical.Distinct().ToList().AsReadOnly();
            }

            if (outOfVocabulary)
            {
                lastFailure = "outOfVocabulary";
                continue;
            }

            var credit = BoardGeometry.AllDirections.Sum(direction =>
                assignment[direction.ToString()]
                    .Count(BenchBoardMapper.ReferenceWords(board, direction).Contains));

            stopwatch.Stop();
            return new BoardDecodeLine(
                Kind: "boardDecode",
                BoardId: board.BoardId,
                Assignment: assignment,
                BoardPositions: credit / (double)SlotsPerBoard,
                BoardSolved: credit == SlotsPerBoard,
                ShuffleSeed: seed.ToString(),
                DecodeFailureKind: null,
                LatencyMs: stopwatch.ElapsedMilliseconds);
        }

        return Failure(board, seed, lastFailure ?? "unparseable", stopwatch);
    }

    private static BoardDecodeLine Failure(
        BenchBoard board, long seed, string failureKind, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new BoardDecodeLine(
            Kind: "boardDecode",
            BoardId: board.BoardId,
            Assignment: null,
            BoardPositions: null,
            BoardSolved: null,
            ShuffleSeed: seed.ToString(),
            DecodeFailureKind: failureKind,
            LatencyMs: stopwatch.ElapsedMilliseconds);
    }

    /// <summary>Rend les 4 listes brutes, ou <c>null</c> si une direction manque ou n'a pas 2 mots.</summary>
    private static Dictionary<string, List<string>>? TryParseAssignment(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("assignment", out var assignment)
                || assignment.ValueKind != JsonValueKind.Object)
                return null;

            var result = new Dictionary<string, List<string>>();
            foreach (var direction in BoardGeometry.AllDirections)
            {
                if (!assignment.TryGetProperty(direction.ToString(), out var words)
                    || words.ValueKind != JsonValueKind.Array)
                    return null;

                var list = words.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!.Trim())
                    .Where(s => s.Length > 0)
                    .Take(2)
                    .ToList();

                if (list.Count != 2)
                    return null;

                result[direction.ToString()] = list;
            }
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
