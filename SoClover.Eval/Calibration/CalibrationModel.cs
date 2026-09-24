using System.Text.Json.Serialization;
using SoClover.Eval.Decoder;
using SoClover.Eval.Prompts;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Ligne 1 de <c>eval/human/calibration.&lt;date&gt;-&lt;empreinte&gt;.jsonl</c> : la trace de
/// reproductibilité de la calibration.
/// <para>
/// <see cref="Epsilon"/> y figure parce que ce <b>n'est pas un réglage libre</b> : le modifier
/// après avoir vu l'accord serait ajuster l'instrument sur sa propre mesure. Toute valeur non
/// nulle doit être décidée <i>avant</i> de lire le résultat — et l'artefact la rend visible.
/// </para>
/// <para>
/// <see cref="CoupleAndAnchorCount"/> = <c>lot.Couples.Count + lot.Anchors.Count</c> — ancres
/// INCLUSES. Ni le même compteur que <c>AgreementReport.CoupleCount</c> (ancres exclues) ni que
/// <c>FamilyAgreement.ScorableCoupleCount</c> (non-scorables exclus) : le nom porte la sémantique
/// pour que l'artefact committé reste lisible sans ce contexte.
/// </para>
/// </summary>
public sealed record CalibrationManifest(
    string Kind,
    string CalibrationId,
    DateTime CreatedAtUtc,
    string ComparisonsFile,
    string BenchFile,
    string BenchHash,
    int CoupleAndAnchorCount,
    int ClueCount,
    string DecoderFingerprint,
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
    int DecodesPerClue,
    double Epsilon,
    int HarnessVersion,
    string? OperatorNotes,
    // Symétrique de DecodeManifest : ce que la machine a servi. Nullables et en fin de record,
    // donc les calibrations déjà committées se relisent inchangées. Hors DecoderFingerprint.
    string? Quantization = null,
    int? LoadedContextLength = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PromptProvenance? CluePrompt = null,
    // Mode de traçage de la session qui a créé l'artefact (phase 3). Omis quand nul : un
    // manifeste antérieur se relit et se re-sérialise à l'identique. Hors hash8 (RunFile.ComputeHash8
    // l'efface) et hors DecoderFingerprint : tracer ne change pas l'identité d'une mesure.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TracingManifest? Tracing = null);

/// <summary>
/// Un décodage de calibration. C'est <see cref="ClueDecodeLine"/> <b>plus le champ
/// <see cref="Clue"/></b>, et le champ n'est pas décoratif : la clé de reprise devient
/// <c>(boardId, direction, clue, decodeIndex)</c>.
/// <para>
/// <see cref="ClueDecodeLine"/> a pour clé <c>(boardId, direction, decodeIndex)</c> — elle suffit
/// à un run, où une direction porte un seul indice. En calibration, une direction porte <b>deux à
/// trois</b> indices concurrents (humain, modèle, assisté) : la réutiliser telle quelle les ferait
/// collisionner silencieusement à la reprise.
/// </para>
/// </summary>
public sealed record CalibrationDecode(
    string Kind,
    string BoardId,
    string Direction,
    string Clue,
    int DecodeIndex,
    IReadOnlyList<string>? Picked,
    double? R,
    string ShuffleSeed,
    string? DecodeFailureKind,
    long LatencyMs)
{
    public const string LineKind = "calibrationDecode";

    /// <summary>
    /// Projette le résultat de <c>ClueDecoder.DecodeAsync</c> — réutilisé <b>tel quel</b> : il
    /// décode un indice quelconque, il n'a jamais rien su du run dont l'indice provient.
    /// </summary>
    public static CalibrationDecode From(ClueDecodeLine line, string clue) => new(
        Kind: LineKind,
        BoardId: line.BoardId,
        Direction: line.Direction,
        Clue: clue,
        DecodeIndex: line.DecodeIndex,
        Picked: line.Picked,
        R: line.R,
        ShuffleSeed: line.ShuffleSeed,
        DecodeFailureKind: line.DecodeFailureKind,
        LatencyMs: line.LatencyMs);
}

public sealed record CalibrationContents(
    CalibrationManifest Manifest,
    IReadOnlyList<CalibrationDecode> Decodes);
