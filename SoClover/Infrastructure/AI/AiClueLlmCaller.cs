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
/// Diagnostic non fatal rencontré en tentant de charger le préambule système du mode reasoning
/// (<see cref="LlmOptions.ReasoningSystemPromptPathEnabler"/>). <paramref name="Cause"/> distingue
/// les deux cas : <c>null</c> = fichier introuvable, non-<c>null</c> = fichier présent mais illisible
/// (verrou, permissions, ...). Porté à la fois par <see cref="ClueCallResult"/> (chemin succès) et
/// par <see cref="LlmCallException"/> (chemin échec) : <see cref="AiClueLlmCaller"/> ne journalise
/// pas, c'est donc le seul canal par lequel l'appelant peut apprendre qu'un préambule attendu n'a
/// pas été injecté, quelle que soit l'issue de l'appel.
/// </summary>
public sealed record ReasoningPreambleWarning(string Path, Exception? Cause);

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
    long? OutputTokens,
    ReasoningPreambleWarning? PreambleWarning);

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
        ReasoningPreambleWarning? preambleWarning = null;
        if (reasoningEnabled)
        {
            var (preamble, warning) = ReadReasoningPreamble(opts.ReasoningSystemPromptPathEnabler);
            preambleWarning = warning;
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
                PreambleWarning = preambleWarning,
            };
        }

        var text = StripJsonFences(StripThinkTags(rawText));

        AiBoardCluesDraft draft;
        try
        {
            draft = parseResponse(text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ne rattrape pas que UnparseableLlmResponseException (le cas normal, émis par
            // AiClueResponseParser) : un parseResponse custom (harnais d'éval, format futur) peut
            // lever n'importe quelle autre exception de parse (NotSupportedException, FormatException,
            // ...) — sans ce filet, elle traverserait sans observabilité (LatencyMs/PromptVersion/
            // EffectiveModel/PreambleWarning) ni typage reconnaissable pour l'appelant. On préserve le
            // RawTextExcerpt et l'InnerException déjà renseignés quand ex est déjà une
            // UnparseableLlmResponseException ; sinon on repart du texte brut et on garde ex en
            // InnerException. L'annulation (OperationCanceledException) est explicitement exclue du
            // filtre : elle ne doit jamais être ré-emballée en échec « JSON invalide ».
            var (rawTextExcerpt, inner) = ex is UnparseableLlmResponseException unparseable
                ? (unparseable.RawTextExcerpt, unparseable.InnerException)
                : (text, ex);

            throw new UnparseableLlmResponseException(rawTextExcerpt, inner)
            {
                LatencyMs = latencyMs,
                PromptVersion = bundle.PromptVersion,
                EffectiveModel = effectiveModel,
                PreambleWarning = preambleWarning,
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
            response.Usage?.OutputTokenCount,
            preambleWarning);
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

    /// <summary>
    /// Charge le préambule système du mode reasoning, avec cache indexé par <c>(Path,
    /// LastWriteTimeUtc)</c> — un vrai hot-reload : le contenu n'est relu que si l'horodatage a
    /// changé. Un fichier absent ou illisible n'est PAS masqué par une entrée de cache antérieure :
    /// il est traité à chaque appel comme un diagnostic à part entière (fidèle au comportement
    /// d'avant l'extraction), distinct selon qu'il s'agit d'une absence ou d'une erreur de lecture.
    /// </summary>
    private (string Content, ReasoningPreambleWarning? Warning) ReadReasoningPreamble(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (string.Empty, null);

        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return (string.Empty, new ReasoningPreambleWarning(path, Cause: null));

            var lastWrite = info.LastWriteTimeUtc;
            if (_reasoningPreambleCache is { } cached
                && cached.Path == path && cached.LastWriteTimeUtc == lastWrite)
                return (cached.Content, null);

            var content = File.ReadAllText(path);
            _reasoningPreambleCache = (path, lastWrite, content);
            return (content, null);
        }
        catch (IOException ex)
        {
            return (string.Empty, new ReasoningPreambleWarning(path, ex));
        }
    }
}
