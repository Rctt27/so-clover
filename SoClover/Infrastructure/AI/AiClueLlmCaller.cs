using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.AI.Reasoning;

namespace SoClover.Infrastructure.AI;

/// <summary>Entrée d'un appel LLM de génération d'indices, sans dépendance au domaine de jeu.</summary>
public sealed record ClueCallRequest(
    string Language,
    IReadOnlyList<BoardCardSnapshot> Cards,
    IReadOnlyList<Direction> RemainingDirections,
    IReadOnlyDictionary<Direction, IReadOnlyList<RejectedAttempt>> RejectedPerDirection,
    string? ModelOverride,
    double? TemperatureOverride);

/// <summary>
/// Sortie d'un appel LLM réussi. <paramref name="RawText"/> est le texte brut du modèle,
/// avant strip des balises de réflexion et des fences — utile au diagnostic.
/// </summary>
public sealed record ClueCallResult(
    AiBoardCluesDraft Draft,
    int? PromptVersion,
    string EffectiveModel,
    long LatencyMs,
    string RawText,
    ChatFinishReason? FinishReason,
    long? InputTokens,
    long? OutputTokens);

/// <summary>
/// Appelant LLM partagé entre la production (via <c>AiCluesGeneratorBase</c>) et le harnais
/// d'évaluation hors ligne — seule façon de garantir que le chiffre mesuré caractérise le code
/// qui tourne en partie.
/// <para>
/// <b>Ne journalise pas.</b> Il rend latence, version de prompt, modèle effectif et usage, et
/// laisse ses appelants décider quoi en faire : la production émet ses messages structurés avec
/// leur contexte <c>game=</c>/<c>player=</c>, le harnais écrit les mêmes données dans son fichier
/// de run.
/// </para>
/// </summary>
public sealed class AiClueLlmCaller
{
    private readonly IChatClient _chatClient;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IReasoningRequestConfigurator _reasoningConfigurator;
    private (string Path, DateTime LastWriteTimeUtc, string Content)? _reasoningPreambleCache;

    public AiClueLlmCaller(
        IChatClient chatClient,
        IOptions<LlmOptions> llmOptions,
        IReasoningRequestConfigurator? reasoningConfigurator = null)
    {
        _chatClient = chatClient;
        _llmOptions = llmOptions;
        _reasoningConfigurator = reasoningConfigurator ?? new NullReasoningConfigurator();
    }

    /// <summary>Chemins de préambule introuvables ou illisibles rencontrés, pour journalisation par l'appelant.</summary>
    public string? LastPreambleWarning { get; private set; }

    public async Task<ClueCallResult> CallAsync(
        ClueCallRequest request,
        IAiCluePromptProvider promptProvider,
        Func<IAiCluePromptProvider, BoardCluesPromptContext, AiCluePromptBundle> buildBundle,
        Func<string, AiBoardCluesDraft> parseResponse,
        CancellationToken ct)
    {
        var opts = _llmOptions.Value;
        var reasoningEnabled = opts.ReasoningEnabled;

        var context = new BoardCluesPromptContext(
            request.Language, request.Cards, request.RemainingDirections,
            request.RejectedPerDirection, IncludeReasoning: reasoningEnabled);
        var bundle = buildBundle(promptProvider, context);

        var systemPrompt = bundle.SystemPrompt;
        if (reasoningEnabled)
        {
            var preamble = ReadReasoningPreamble(opts.ReasoningSystemPromptPathEnabler);
            if (!string.IsNullOrWhiteSpace(preamble))
                systemPrompt = $"{preamble.Trim()}\n\n{systemPrompt}";
        }

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, bundle.UserPrompt),
        };

        var effectiveModel = request.ModelOverride ?? opts.DefaultModel;

        var chatOptions = new ChatOptions
        {
            ModelId = effectiveModel,
            Temperature = (float)(request.TemperatureOverride ?? opts.DefaultTemperature),
        };
        if (opts.TopP is { } topP)
            chatOptions.TopP = (float)topP;
        if (opts.MaxOutputTokens is { } maxOutputTokens)
            chatOptions.MaxOutputTokens = maxOutputTokens;

        if (reasoningEnabled)
            _reasoningConfigurator.Configure(chatOptions);

        var sw = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages, options: chatOptions, ct)
            .ConfigureAwait(false);
        sw.Stop();

        var latencyMs = sw.ElapsedMilliseconds;
        var rawText = response.Text;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new EmptyLlmResponseException(
                response.FinishReason,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount)
            {
                LatencyMs = latencyMs,
                PromptVersion = bundle.PromptVersion,
                EffectiveModel = effectiveModel,
            };
        }

        var text = StripJsonFences(StripThinkTags(rawText));

        AiBoardCluesDraft draft;
        try
        {
            draft = parseResponse(text);
        }
        catch (UnparseableLlmResponseException ex)
        {
            throw new UnparseableLlmResponseException(ex.RawTextExcerpt, ex.InnerException)
            {
                LatencyMs = latencyMs,
                PromptVersion = bundle.PromptVersion,
                EffectiveModel = effectiveModel,
            };
        }

        return new ClueCallResult(
            draft,
            bundle.PromptVersion,
            effectiveModel,
            latencyMs,
            rawText,
            response.FinishReason,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);
    }

    private static string StripJsonFences(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```")) return t;

        var firstNewline = t.IndexOf('\n');
        if (firstNewline >= 0) t = t[(firstNewline + 1)..];
        if (t.EndsWith("```")) t = t[..^3];
        return t.Trim();
    }

    private static string StripThinkTags(string text)
    {
        foreach (var closeTag in new[] { "</think>", "[/THINK]" })
        {
            var closeIdx = text.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
            if (closeIdx >= 0)
                text = text[(closeIdx + closeTag.Length)..];
        }
        return text.Trim();
    }

    private string ReadReasoningPreamble(string? path)
    {
        LastPreambleWarning = null;
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                // Fichier disparu depuis le dernier appel réussi : on retombe sur le cache (indexé
                // par Path seul ici, faute de LastWriteTimeUtc à comparer) plutôt que de traiter une
                // absence transitoire comme une erreur — ne relit jamais le disque dans ce cas.
                if (_reasoningPreambleCache is { } cachedMissing && cachedMissing.Path == path)
                    return cachedMissing.Content;

                LastPreambleWarning = path;
                return string.Empty;
            }

            var lastWrite = info.LastWriteTimeUtc;
            if (_reasoningPreambleCache is { } cached
                && cached.Path == path && cached.LastWriteTimeUtc == lastWrite)
                return cached.Content;

            var content = File.ReadAllText(path);
            _reasoningPreambleCache = (path, lastWrite, content);
            return content;
        }
        catch (IOException)
        {
            LastPreambleWarning = path;
            return string.Empty;
        }
    }
}
