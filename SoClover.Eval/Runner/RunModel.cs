using System.Text.Json.Serialization;
using SoClover.Eval.Prompts;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Runner;

/// <summary>
/// Ligne 1 d'un fichier de run : configuration <b>observée</b> (effective après résolution),
/// jamais la configuration déclarée. C'est la trace de reproductibilité du run.
/// </summary>
public sealed record RunManifest(
    string Kind,
    string RunId,
    string Stage,
    DateTime CreatedAtUtc,
    string BenchFile,
    string BenchHash,
    string? PromptFile,
    int? PromptVersion,
    string GenerationMode,
    bool ReasoningEnabled,
    string Provider,
    string BaseUrl,
    string ModelId,
    string? ModelSnapshotDate,
    string? ProviderModelListHash,
    double Temperature,
    double? TopP,
    int? MaxOutputTokens,
    int MaxRetries,
    string Language,
    int HarnessVersion,
    string? OperatorNotes,
    // Provenance du prompt générateur (spec §6.4). Omise quand nulle : un manifeste sans elle se
    // sérialise exactement comme avant, et RunFile.ComputeHash8 l'efface : la provenance elle-même
    // est hors hash8. Le hash8 hache en revanche PromptFile, dont le chemin dépend de la source
    // (eval/prompts/resolved/<sha12>/… pour Langfuse, bin/Debug|Release/… pour le fichier) : deux
    // re-runs de même configuration servis par des sources différentes n'ont pas le même hash8.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PromptProvenance? Prompt = null,
    // Mode de traçage de la session qui a créé l'artefact (phase 3). Omis quand nul : un
    // manifeste antérieur se relit et se re-sérialise à l'identique. Hors hash8 (RunFile.ComputeHash8
    // l'efface) et hors DecoderFingerprint : tracer ne change pas l'identité d'une mesure.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TracingManifest? Tracing = null);

/// <summary>
/// Une tentative d'appel LLM pour une direction. <b>Une ligne par tentative</b>, jamais une ligne
/// par direction : la résolution au niveau direction est dérivée par le scorer, ce qui laisse un
/// seul invariant à maintenir et rend la reprise triviale.
/// <para>
/// <c>failureKind ∈ { null, "empty", "unparseable", "directionMismatch", "timeout", "transport" }</c>.
/// Quand il est non nul, <see cref="Clue"/> est <c>null</c> et <see cref="Valid"/> est <c>false</c>.
/// </para>
/// </summary>
public sealed record RunAttempt(
    string Kind,
    string BoardId,
    string Direction,
    int Attempt,
    string? Clue,
    IReadOnlyList<string>? Candidates,
    string? Explanation,
    bool Valid,
    IReadOnlyList<string> RejectionRules,
    string? FailureKind,
    long LatencyMs,
    long? InputTokens,
    long? OutputTokens,
    int? PromptVersion,
    string EffectiveModel);

public sealed record RunContents(
    RunManifest Manifest,
    IReadOnlyList<RunAttempt> Attempts);
