using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Decoder;

/// <summary>
/// Décodeur aveugle mono-indice (N2). Reçoit l'indice et les 16 mots dans un ordre déterministe
/// dérivé de <c>(benchHash, boardId, decodeIndex)</c>, et doit désigner exactement 2 mots.
/// <para>
/// Il ne voit <b>jamais</b> : la paire de référence, l'<c>explanation</c> du générateur, ses
/// <c>candidates</c>, ni les autres indices du board.
/// </para>
/// <para>
/// Prompt versionné <b>indépendamment</b> du prompt générateur, dans un fichier dédié chargé par
/// le <see cref="FilePromptLoader"/> existant.
/// </para>
/// </summary>
public sealed class ClueDecoder
{
    private const int MaxFormatAttempts = 2; // 1 essai + 1 retry, avec le MÊME ordre de présentation

    private readonly IChatClient _chatClient;
    private readonly ParsedPromptSections _sections;
    private readonly ChatOptions _chatOptions;

    public ClueDecoder(
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

    public async Task<ClueDecodeLine> DecodeAsync(
        BenchBoard board,
        Direction direction,
        string clue,
        int decodeIndex,
        string benchHash,
        CancellationToken ct)
    {
        var seed = ShuffleSeed.ForClue(benchHash, board.BoardId, decodeIndex);
        var presented = ShuffleSeed.Shuffle(BenchBoardMapper.AllWords(board), seed);
        var byNormalized = presented.ToDictionary(TextNormalizer.Normalize, w => w);

        // Deux rendus des mêmes seize mots, dérivés de la même graine par deux instances de PRNG
        // distinctes : à plat (v2-v4) et groupés par carte (v5). Une version de prompt n'emploie
        // qu'un seul des deux placeholders ; substituer les deux garde les anciennes versions
        // reproductibles à l'identique.
        var userPrompt = new StringBuilder(_sections.User)
            .Replace("{{shuffledBoardWords}}", string.Join("\n", presented.Select(w => $"- {w}")))
            .Replace("{{cardGroupedBoardWords}}", ShuffleSeed.RenderByCard(
                ShuffleSeed.ShuffleByCard(board.Cards, seed)))
            .Replace("{{clueWord}}", clue)
            .ToString()
            .Trim();

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, _sections.System.Trim()),
            new ChatMessage(ChatRole.User, userPrompt),
        };

        var stopwatch = Stopwatch.StartNew();
        string? lastFailure = null;

        // Retenté UNE fois avec le même ordre de présentation : le mélange dépend de
        // decodeIndex, seul l'échantillonnage du modèle change.
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
                return Failure(board, direction, decodeIndex, seed, "timeout", stopwatch);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                Console.Error.WriteLine($"AVERTISSEMENT : décodage — erreur de transport ({ex.Message}).");
                return Failure(board, direction, decodeIndex, seed, "transport", stopwatch);
            }

            var text = response.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                lastFailure = "empty";
                continue;
            }

            var picked = TryParsePicked(StripFences(StripThinkTags(text)), out var parseFailure);
            if (picked is null)
            {
                lastFailure = parseFailure;
                continue;
            }

            var canonical = new List<string>();
            var outOfVocabulary = false;
            foreach (var word in picked)
            {
                if (byNormalized.TryGetValue(TextNormalizer.Normalize(word), out var boardWord))
                    canonical.Add(boardWord);
                else
                    outOfVocabulary = true;
            }

            if (outOfVocabulary)
            {
                lastFailure = "outOfVocabulary";
                continue;
            }

            var distinct = canonical.Distinct().ToList();
            if (distinct.Count != 2)
            {
                lastFailure = "tooFewWords";
                continue;
            }

            var reference = BenchBoardMapper.ReferenceWords(board, direction);
            var hits = distinct.Count(reference.Contains);
            stopwatch.Stop();

            return new ClueDecodeLine(
                Kind: "decode",
                BoardId: board.BoardId,
                Direction: direction.ToString(),
                DecodeIndex: decodeIndex,
                Picked: distinct.AsReadOnly(),
                R: hits / 2.0,
                ShuffleSeed: seed.ToString(),
                DecodeFailureKind: null,
                LatencyMs: stopwatch.ElapsedMilliseconds);
        }

        return Failure(board, direction, decodeIndex, seed, lastFailure ?? "unparseable", stopwatch);
    }

    private static ClueDecodeLine Failure(
        BenchBoard board, Direction direction, int decodeIndex, long seed,
        string failureKind, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new ClueDecodeLine(
            Kind: "decode",
            BoardId: board.BoardId,
            Direction: direction.ToString(),
            DecodeIndex: decodeIndex,
            Picked: null,
            R: null,
            ShuffleSeed: seed.ToString(),
            DecodeFailureKind: failureKind,
            LatencyMs: stopwatch.ElapsedMilliseconds);
    }

    private static IReadOnlyList<string>? TryParsePicked(string text, out string failureKind)
    {
        failureKind = "unparseable";
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("picked", out var picked)
                || picked.ValueKind != JsonValueKind.Array)
                return null;

            var words = picked.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (words.Count < 2)
            {
                failureKind = "tooFewWords";
                return null;
            }

            return words.Take(2).ToList().AsReadOnly();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string StripFences(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```")) return t;
        var firstNewline = t.IndexOf('\n');
        if (firstNewline >= 0) t = t[(firstNewline + 1)..];
        if (t.EndsWith("```")) t = t[..^3];
        return t.Trim();
    }

    internal static string StripThinkTags(string text)
    {
        foreach (var closeTag in new[] { "</think>", "[/THINK]" })
        {
            var idx = text.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) text = text[(idx + closeTag.Length)..];
        }
        return text.Trim();
    }
}
