using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Garde de reprise de <c>decode</c>. Elle ne comparait que <c>decodesPerClue</c> : reprendre un
/// décodage partiel après avoir changé de modèle, de prompt ou de température ajoutait les
/// nouvelles lignes sous un manifeste qui ne nomme que le <b>premier</b> décodeur. Le fichier
/// devenait un mélange indétectable, et <c>DecoderFingerprint.FromManifest</c> rendait une
/// empreinte fausse pour la moitié de ses lignes.
/// <para>
/// Depuis que le chemin porte l'empreinte, la collision est déjà improbable — mais un fichier
/// renommé à la main, ou copié d'une machine à l'autre, la ramène. La garde vérifie donc
/// l'empreinte complète, pas seulement le nombre de décodages. Symétrie assumée avec
/// <c>CalibrateCommand.RequireCompatibleResume</c>.
/// </para>
/// </summary>
public class DecodeResumeGuardTests
{
    private const string Path = "eval/runs/run-x.9a829dc206d2.decoded.jsonl";

    private static DecodeManifest Manifest(
        string model = "qwen/qwen3-8b",
        double temperature = 0.3,
        int? maxOutputTokens = 512,
        int cluePromptVersion = 2,
        int decodesPerClue = 3) =>
        new("manifest", "decode-1", new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            "run-x", "eval/boards.dev.jsonl", "416b819a41a1",
            "OpenAI", "http://localhost:1234/v1", model, "2026-08-06", null,
            temperature, null, maxOutputTokens,
            "Decoder/Prompts/fr/decode-clue.md", cluePromptVersion,
            "Decoder/Prompts/fr/decode-board.md", 1,
            decodesPerClue, RunFile.HarnessVersion, null);

    private static string FingerprintOf(DecodeManifest m) =>
        SoClover.Eval.Calibration.DecoderFingerprint.FromManifest(m);

    [Fact]
    public void Une_reprise_sous_le_meme_decodeur_et_le_meme_nombre_de_decodages_passe()
    {
        var manifest = Manifest();

        DecodeCommand.RequireCompatibleResume(manifest, FingerprintOf(manifest), 3, Path);
    }

    [Fact]
    public void Un_nombre_de_decodages_different_est_refuse()
    {
        var manifest = Manifest(decodesPerClue: 3);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DecodeCommand.RequireCompatibleResume(manifest, FingerprintOf(manifest), 5, Path));

        Assert.Contains("decodesPerClue", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--force", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("modèle")]
    [InlineData("température")]
    [InlineData("maxOutputTokens")]
    [InlineData("version de prompt")]
    public void Tout_changement_de_decodeur_est_refuse(string axis)
    {
        var existing = Manifest();
        var courant = axis switch
        {
            "modèle" => Manifest(model: "mistral/ministral-14b"),
            "température" => Manifest(temperature: 0.7),
            "maxOutputTokens" => Manifest(maxOutputTokens: 1024),
            _ => Manifest(cluePromptVersion: 3),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DecodeCommand.RequireCompatibleResume(existing, FingerprintOf(courant), 3, Path));

        // Le message nomme les DEUX empreintes : sans elles, l'opérateur ne sait pas laquelle
        // des cinq composantes a bougé, ni laquelle est celle qu'il croyait charger.
        Assert.Contains(FingerprintOf(existing), ex.Message, StringComparison.Ordinal);
        Assert.Contains(FingerprintOf(courant), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>decodesPerClue</c> est délibérément hors de l'empreinte (il change la granularité de R̄,
    /// pas le décodeur). La garde doit donc porter les deux vérifications séparément — une seule
    /// laisserait passer la moitié des cas.
    /// </summary>
    [Fact]
    public void Le_decodeur_et_le_nombre_de_decodages_sont_deux_verifications_distinctes()
    {
        var existing = Manifest(decodesPerClue: 3);
        var autreDecodeur = FingerprintOf(Manifest(model: "autre/modele"));

        // Même decodesPerClue, décodeur différent : refusé malgré l'accord sur le nombre.
        Assert.Throws<InvalidOperationException>(() =>
            DecodeCommand.RequireCompatibleResume(existing, autreDecodeur, 3, Path));
    }
}
