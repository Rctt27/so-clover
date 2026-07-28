using System.Net.Sockets;
using System.Runtime.CompilerServices;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Runner;

/// <summary>
/// Boucle de génération d'indices sur un banc, en mode PerDirection.
/// <para>
/// Les deux modes d'échec de l'appel sont attrapés <b>séparément</b> pour renseigner
/// <c>failureKind</c> — c'est ce qui permet au scorer de distinguer <c>parse_failure_rate</c>
/// d'un échec de génération.
/// </para>
/// <para>
/// Règle A-1 du PRD, versant automatique : une direction qui échoue est <b>consignée</b>, jamais
/// retirée du banc. Retirer les cas durs, c'est retirer la résolution de l'instrument.
/// </para>
/// </summary>
public sealed class ClueRunner
{
    private readonly AiClueLlmCaller _caller;
    private readonly IAiCluePromptProvider _promptProvider;
    private readonly IClueValidator _validator;
    private readonly int _maxAttempts;
    private readonly string _language;

    public ClueRunner(
        AiClueLlmCaller caller,
        IAiCluePromptProvider promptProvider,
        IClueValidator validator,
        int maxAttempts,
        string language)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "maxAttempts doit être ≥ 1.");

        _caller = caller;
        _promptProvider = promptProvider;
        _validator = validator;
        _maxAttempts = maxAttempts;
        _language = language;
    }

    public async IAsyncEnumerable<RunAttempt> RunDirectionAsync(
        BenchBoard board,
        Direction direction,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var cards = BenchBoardMapper.ToSnapshots(board);
        var cloverBoard = BenchBoardMapper.ToCloverBoard(board);
        var rejected = new List<RejectedAttempt>();

        for (var attempt = 0; attempt < _maxAttempts; attempt++)
        {
            var rejectedPerDirection = new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>();
            if (rejected.Count > 0)
                rejectedPerDirection[direction] = rejected.AsReadOnly();

            var request = new ClueCallRequest(
                _language, cards, [direction], rejectedPerDirection,
                ModelOverride: null, TemperatureOverride: null);

            // yield return est interdit dans un try/catch : l'appel est isolé dans un helper
            // qui rend un résultat discriminé, et l'émission se fait hors du bloc protégé.
            var outcome = await TryCallAsync(request, ct).ConfigureAwait(false);

            if (outcome.FailureKind is { } transportFailure)
            {
                yield return Failure(board, direction, attempt, transportFailure, outcome);
                continue;
            }

            var result = outcome.Result!;
            var matched = result.Draft.Clues.FirstOrDefault(item =>
                Enum.TryParse<Direction>(item.Direction, ignoreCase: true, out var parsed)
                && parsed == direction);

            if (matched is null)
            {
                yield return Failure(board, direction, attempt, "directionMismatch", outcome);
                continue;
            }

            if (string.IsNullOrWhiteSpace(matched.ClueWord))
            {
                // Le modèle a produit du JSON valide sans indice utilisable : c'est un défaut de
                // format, au même titre qu'un JSON illisible.
                yield return Failure(board, direction, attempt, "unparseable", outcome);
                continue;
            }

            var validation = ClueAcceptance.Check(matched.ClueWord, direction, cloverBoard, _validator);

            yield return new RunAttempt(
                Kind: "attempt",
                BoardId: board.BoardId,
                Direction: direction.ToString(),
                Attempt: attempt,
                Clue: matched.ClueWord,
                Candidates: matched.Candidates,
                Explanation: matched.Explanation,
                Valid: validation.IsValid,
                RejectionRules: validation.Errors.Select(e => e.Rule.ToString()).ToList().AsReadOnly(),
                FailureKind: null,
                LatencyMs: result.LatencyMs,
                InputTokens: result.InputTokens,
                OutputTokens: result.OutputTokens,
                PromptVersion: result.PromptVersion,
                EffectiveModel: result.EffectiveModel);

            if (validation.IsValid)
                yield break;

            rejected.Add(new RejectedAttempt(
                matched.ClueWord, _promptProvider.FormatRejectionReason(validation)));
        }
    }

    private async Task<CallOutcome> TryCallAsync(ClueCallRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _caller.CallAsync(
                request,
                _promptProvider,
                static (p, ctx) => p.BuildSingleDirectionCluePrompt(ctx),
                AiClueResponseParser.ParseSingleDirection,
                ct).ConfigureAwait(false);

            return new CallOutcome(result, null, result.LatencyMs, result.PromptVersion,
                result.EffectiveModel, result.InputTokens, result.OutputTokens);
        }
        catch (EmptyLlmResponseException ex)
        {
            return new CallOutcome(null, "empty", ex.LatencyMs, ex.PromptVersion,
                ex.EffectiveModel, ex.InputTokens, ex.OutputTokens);
        }
        catch (UnparseableLlmResponseException ex)
        {
            return new CallOutcome(null, "unparseable", ex.LatencyMs, ex.PromptVersion,
                ex.EffectiveModel, null, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // TimeoutChatClient combine le token de l'appelant avec un CTS local : une annulation
            // sans demande de l'appelant est un dépassement de délai.
            return new CallOutcome(null, "timeout", 0, null, string.Empty, null, null);
        }
        catch (Exception ex) when (IsTransportFailure(ex))
        {
            Console.Error.WriteLine($"AVERTISSEMENT : erreur de transport ({ex.Message}), le run continue.");
            return new CallOutcome(null, "transport", 0, null, string.Empty, null, null);
        }
    }

    /// <summary>
    /// Vrai si l'échec est imputable au transport, y compris <b>enveloppé</b>.
    /// <para>
    /// Le SDK du provider n'expose pas la connexion refusée telle quelle : côté OpenAI/LM Studio,
    /// la politique de retry de <c>System.ClientModel</c> la présente en
    /// <see cref="AggregateException"/> « Retry failed after N tries ». Un filtre sur le seul type
    /// de surface laissait donc échapper le cas le plus banal — LM Studio arrêté — et faisait
    /// planter le run entier au premier board au lieu de consigner la direction en échec et de
    /// continuer (règle A-1). L'inspection reste ciblée : une exception sans cause de transport
    /// dans sa chaîne remonte intacte, pour qu'un défaut de programmation ne se déguise jamais en
    /// panne réseau dans le fichier de run.
    /// </para>
    /// </summary>
    private static bool IsTransportFailure(Exception ex) => ex switch
    {
        HttpRequestException or IOException or SocketException => true,
        AggregateException aggregate => aggregate.InnerExceptions.Any(IsTransportFailure),
        { InnerException: { } inner } => IsTransportFailure(inner),
        _ => false,
    };

    private static RunAttempt Failure(
        BenchBoard board, Direction direction, int attempt, string failureKind, CallOutcome outcome) =>
        new(
            Kind: "attempt",
            BoardId: board.BoardId,
            Direction: direction.ToString(),
            Attempt: attempt,
            Clue: null,
            Candidates: null,
            Explanation: null,
            Valid: false,
            RejectionRules: [],
            FailureKind: failureKind,
            LatencyMs: outcome.LatencyMs,
            InputTokens: outcome.InputTokens,
            OutputTokens: outcome.OutputTokens,
            PromptVersion: outcome.PromptVersion,
            EffectiveModel: outcome.EffectiveModel);

    private sealed record CallOutcome(
        ClueCallResult? Result,
        string? FailureKind,
        long LatencyMs,
        int? PromptVersion,
        string EffectiveModel,
        long? InputTokens,
        long? OutputTokens);
}
