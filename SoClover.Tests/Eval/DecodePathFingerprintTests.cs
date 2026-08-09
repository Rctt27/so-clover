using SoClover.Eval.Calibration;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Le chemin d'un décodage porte l'empreinte du décodeur qui l'a produit.
/// <para>
/// Constat déclencheur (calibration du 2026-08-06, décodeur renvoyé en P3) : itérer en P3 signifie
/// produire plusieurs variantes de décodeur sur les <b>mêmes</b> runs générateurs. Or
/// <c>PathFor</c> était aveugle à l'empreinte — la première variante essayée écrasait les
/// décodages de référence, et avec eux les <c>.metrics.json</c> dont <c>calibrate</c> tire les
/// portes de plancher et de non-saturation. Un fichier détruit vaut ici plusieurs centaines
/// d'appels au LLM.
/// </para>
/// <para>
/// L'empreinte s'insère <b>avant</b> le suffixe, dans les deux noms, pour préserver la relation
/// de fratrie <c>&lt;run&gt;.&lt;fp&gt;.metrics.json</c> ↔ <c>&lt;run&gt;.&lt;fp&gt;.decoded.jsonl</c>
/// dont dépend <see cref="CalibrationGates.FingerprintOfMetrics"/>.
/// </para>
/// </summary>
public class DecodePathFingerprintTests : IDisposable
{
    private const string Fingerprint = "9a829dc206d2";
    private const string OtherFingerprint = "0011223344ff";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"decode-path-{Guid.NewGuid():N}");

    private string RunPath => Path.Combine(_directory, "20260727-v5-gemma-a1b2c3d4.jsonl");

    public DecodePathFingerprintTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private void Touch(string path) => File.WriteAllText(path, "{}\n");

    [Fact]
    public void PathFor_insere_l_empreinte_avant_le_suffixe()
    {
        Assert.Equal(
            Path.Combine(_directory, $"20260727-v5-gemma-a1b2c3d4.{Fingerprint}.decoded.jsonl"),
            DecodeFile.PathFor(RunPath, Fingerprint));
    }

    [Fact]
    public void Deux_empreintes_donnent_deux_chemins_distincts()
    {
        Assert.NotEqual(
            DecodeFile.PathFor(RunPath, Fingerprint),
            DecodeFile.PathFor(RunPath, OtherFingerprint));
    }

    /// <summary>
    /// La fratrie est l'invariant dont dépend <c>FingerprintOfMetrics</c> : le
    /// <c>.metrics.json</c> ne porte aucune empreinte, on la lit dans son frère.
    /// </summary>
    [Fact]
    public void Le_metrics_est_le_frere_du_decodage_et_l_aller_retour_est_stable()
    {
        var decoded = DecodeFile.PathFor(RunPath, Fingerprint);
        var metrics = DecodeFile.MetricsPathFor(decoded);

        Assert.Equal(
            Path.Combine(_directory, $"20260727-v5-gemma-a1b2c3d4.{Fingerprint}.metrics.json"),
            metrics);
        Assert.Equal(decoded, CalibrationGates.DecodedPathForMetrics(metrics));
    }

    /// <summary>
    /// <c>compare</c> accepte qu'on lui désigne un <c>.decoded.jsonl</c> et remonte au run
    /// générateur. Avant l'empreinte, il retirait le seul suffixe et rendait
    /// <c>&lt;run&gt;.&lt;fp&gt;.jsonl</c> — un fichier qui n'existe pas.
    /// </summary>
    [Fact]
    public void RunPathFor_retire_l_empreinte_et_le_suffixe()
    {
        Assert.Equal(RunPath, DecodeFile.RunPathFor(DecodeFile.PathFor(RunPath, Fingerprint)));
    }

    [Fact]
    public void RunPathFor_gere_le_decodage_hérité_sans_empreinte()
    {
        Assert.Equal(
            RunPath,
            DecodeFile.RunPathFor(Path.Combine(_directory, "20260727-v5-gemma-a1b2c3d4.decoded.jsonl")));
    }

    /// <summary>
    /// Un segment qui n'a pas la forme d'une empreinte (12 hexa) appartient à l'identifiant du
    /// run, et se retirer serait une mutilation silencieuse du chemin.
    /// </summary>
    [Fact]
    public void RunPathFor_ne_retire_pas_un_segment_qui_n_est_pas_une_empreinte()
    {
        var path = Path.Combine(_directory, "run.v2.decoded.jsonl");

        Assert.Equal(Path.Combine(_directory, "run.v2.jsonl"), DecodeFile.RunPathFor(path));
    }

    [Fact]
    public void FindForRun_rend_null_quand_aucun_decodage_n_existe()
    {
        Assert.Null(DecodeFile.FindForRun(RunPath));
    }

    [Fact]
    public void FindForRun_trouve_l_unique_decodage_empreinte()
    {
        var expected = DecodeFile.PathFor(RunPath, Fingerprint);
        Touch(expected);

        Assert.Equal(expected, DecodeFile.FindForRun(RunPath));
    }

    /// <summary>
    /// Les décodages produits avant ce changement s'appellent <c>&lt;run&gt;.decoded.jsonl</c>.
    /// Ils coûtent trop cher pour être rendus invisibles par un renommage de convention.
    /// </summary>
    [Fact]
    public void FindForRun_accepte_encore_un_decodage_hérité_sans_empreinte()
    {
        var legacy = Path.Combine(_directory, "20260727-v5-gemma-a1b2c3d4.decoded.jsonl");
        Touch(legacy);

        Assert.Equal(legacy, DecodeFile.FindForRun(RunPath));
    }

    /// <summary>
    /// Le cœur du correctif : dès que deux décodeurs coexistent, aucun choix implicite n'est
    /// défendable. Le refus est bruyant et nomme les candidats.
    /// </summary>
    [Fact]
    public void FindForRun_refuse_de_choisir_entre_deux_decodeurs()
    {
        Touch(DecodeFile.PathFor(RunPath, Fingerprint));
        Touch(DecodeFile.PathFor(RunPath, OtherFingerprint));

        var ex = Assert.Throws<InvalidOperationException>(() => DecodeFile.FindForRun(RunPath));

        Assert.Contains(Fingerprint, ex.Message, StringComparison.Ordinal);
        Assert.Contains(OtherFingerprint, ex.Message, StringComparison.Ordinal);
        Assert.Contains("--decoded", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindForRun_refuse_aussi_le_melange_herite_et_empreinte()
    {
        Touch(Path.Combine(_directory, "20260727-v5-gemma-a1b2c3d4.decoded.jsonl"));
        Touch(DecodeFile.PathFor(RunPath, Fingerprint));

        Assert.Throws<InvalidOperationException>(() => DecodeFile.FindForRun(RunPath));
    }

    /// <summary>
    /// Un run voisin dont l'identifiant <b>préfixe</b> celui qu'on cherche ne doit pas être
    /// ramassé par la recherche : <c>run-1</c> et <c>run-10</c> coexistent au registre.
    /// </summary>
    [Fact]
    public void FindForRun_ne_ramasse_pas_le_decodage_d_un_run_voisin()
    {
        var neighbour = Path.Combine(_directory, $"20260727-v5-gemma-a1b2c3d4-bis.{Fingerprint}.decoded.jsonl");
        Touch(neighbour);

        Assert.Null(DecodeFile.FindForRun(RunPath));
    }
}
