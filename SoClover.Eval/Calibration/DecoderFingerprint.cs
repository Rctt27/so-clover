using System.Globalization;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Empreinte de la configuration du décodeur : ce qui rend deux <c>recovery</c> comparables.
/// <para>
/// Le constat déclencheur du cycle P6 : les <c>.decoded.jsonl</c> committés portent
/// <c>cluePromptVersion: 1</c> alors que <c>decode-clue.md</c> est en v2. <b>Le décodeur qui a
/// produit <c>recovery = 0,363</c> n'existe plus.</b> Sans empreinte, une porte serait franchie
/// sur un instrument et des chiffres publiés par un autre.
/// </para>
/// <para>
/// <c>decodesPerClue</c> en est <b>délibérément exclu</b> : il change la granularité de R̄, pas le
/// décodeur. C'est ce qui autorise une calibration à 5 décodages et des runs à 3 sans que les
/// empreintes divergent. Le nombre de décodages reste consigné à part, dans les deux manifestes.
/// </para>
/// </summary>
public static class DecoderFingerprint
{
    /// <summary>Même longueur que <c>benchHash</c> : les deux se lisent côte à côte au registre.</summary>
    public const int HexLength = 12;

    /// <summary>Séparateur de champs du texte haché : U+001F, impossible dans un identifiant de modèle.</summary>
    private const string FieldSeparator = "";

    private const string Absent = "—";

    public static string Compute(
        string modelId,
        string cluePromptFile,
        int? cluePromptVersion,
        double temperature,
        double? topP,
        int? maxOutputTokens)
    {
        string[] fields =
        [
            modelId,
            CanonicalPromptPath(cluePromptFile),
            cluePromptVersion?.ToString(CultureInfo.InvariantCulture) ?? Absent,
            temperature.ToString("R", CultureInfo.InvariantCulture),
            topP?.ToString("R", CultureInfo.InvariantCulture) ?? Absent,
            maxOutputTokens?.ToString(CultureInfo.InvariantCulture) ?? Absent,
        ];

        return EvalJson.Sha256Hex(string.Join(FieldSeparator, fields))[..HexLength];
    }

    public static string FromManifest(DecodeManifest manifest) => Compute(
        manifest.ModelId,
        manifest.CluePromptFile,
        manifest.CluePromptVersion,
        manifest.Temperature,
        manifest.TopP,
        manifest.MaxOutputTokens);

    /// <summary>
    /// Les <b>deux derniers segments</b> du chemin, en <c>/</c> et en minuscules :
    /// <c>fr/decode-clue.md</c>.
    /// <para>
    /// <c>DecodeManifest.CluePromptFile</c> est un chemin <b>absolu</b> dérivé de
    /// <c>AppContext.BaseDirectory</c>. Haché tel quel, l'empreinte changerait d'une machine à
    /// l'autre, et même d'un <c>bin/Debug</c> à un <c>bin/Release</c> : deux calibrations
    /// identiques deviendraient incomparables. Deux segments et non un seul, parce que
    /// <c>en/decode-clue.md</c> doit être une autre empreinte.
    /// </para>
    /// </summary>
    internal static string CanonicalPromptPath(string path)
    {
        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tail = segments.TakeLast(2);
        return string.Join('/', tail).ToLowerInvariant();
    }
}
