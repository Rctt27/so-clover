namespace SoClover.Eval.Decoder;

/// <summary>
/// Ligne 1 d'un fichier <c>&lt;runId&gt;.decoded.jsonl</c>. Fichier <b>distinct</b> du run
/// générateur, jamais une réécriture en place : le run reste la trace intacte de ce qu'a
/// produit le modèle générateur.
/// </summary>
public sealed record DecodeManifest(
    string Kind,
    string DecodeRunId,
    DateTime CreatedAtUtc,
    string GeneratorRunId,
    string BenchFile,
    string BenchHash,
    string Provider,
    string BaseUrl,
    string ModelId,
    string? ModelSnapshotDate,
    string? ProviderModelListHash,
    double Temperature,
    double? TopP,
    int? MaxOutputTokens,
    string CluePromptFile,
    int? CluePromptVersion,
    string BoardPromptFile,
    int? BoardPromptVersion,
    int DecodesPerClue,
    int HarnessVersion,
    string? OperatorNotes,
    // Ce que la machine a servi, par opposition aux champs ci-dessus qui disent ce qu'on a
    // demandé. Nullables et en fin de record : les manifestes antérieurs se relisent inchangés,
    // avec ces deux champs à null. Volontairement HORS DecoderFingerprint — voir
    // ModelRuntimeProbe et ModelRuntimeProbeTests.
    string? Quantization = null,
    int? LoadedContextLength = null);

/// <summary>
/// Un décodage mono-indice (N2). <c>R = |picked ∩ referenceWords| / 2 ∈ {0, 0.5, 1}</c>.
/// <para>
/// <c>decodeFailureKind ∈ { null, "unparseable", "outOfVocabulary", "tooFewWords", "empty",
/// "timeout", "transport" }</c>. Quand il est non nul, <see cref="Picked"/> et <see cref="R"/>
/// sont <c>null</c> : le décodage est <b>exclu du dénominateur de R̄</b>, pas compté 0 — un
/// décodeur qui ne sait pas répondre au format n'est pas un décodeur qui se trompe.
/// </para>
/// </summary>
public sealed record ClueDecodeLine(
    string Kind,
    string BoardId,
    string Direction,
    int DecodeIndex,
    IReadOnlyList<string>? Picked,
    double? R,
    string ShuffleSeed,
    string? DecodeFailureKind,
    long LatencyMs);

/// <summary>
/// Un décodage board complet (N3) : affectation de 2 mots à chacune des 4 directions.
/// <c>boardPositions</c> = crédit sommé sur les 4 directions, divisé par 8.
/// <para>
/// <see cref="BoardSolved"/> garde ce nom alors que la métrique agrégée s'appelle
/// <c>board_solved_first_try</c> : ici la notion est <b>tautologique</b> — une ligne
/// <c>boardDecode</c> est le résultat d'une passe unique, il n'y a pas de seconde tentative dont
/// se distinguer. Renommer le champ changerait le schéma des <c>.decoded.jsonl</c> déjà produits,
/// que <c>EvalJson</c> relirait <b>silencieusement</b> avec la valeur à <c>null</c> (les options
/// de sérialisation ne rejettent pas les champs absents). Le gain de clarté est nul, le risque de
/// corruption silencieuse ne l'est pas.
/// </para>
/// </summary>
public sealed record BoardDecodeLine(
    string Kind,
    string BoardId,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Assignment,
    double? BoardPositions,
    bool? BoardSolved,
    string ShuffleSeed,
    string? DecodeFailureKind,
    long LatencyMs);

public sealed record DecodeContents(
    DecodeManifest Manifest,
    IReadOnlyList<ClueDecodeLine> ClueDecodes,
    IReadOnlyList<BoardDecodeLine> BoardDecodes);
