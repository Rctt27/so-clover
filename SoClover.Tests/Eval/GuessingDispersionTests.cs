using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Séance E : la dispersion entre <b>deux</b> humains devineurs, contre l'écart humain / décodeur.
/// Le critère et ses issues sont <b>pré-enregistrés au registre le 2026-08-08</b>, avant que H2
/// n'ait deviné quoi que ce soit — ces tests les figent, ils ne les choisissent pas.
/// </summary>
public class GuessingDispersionTests
{
    private static GuessingLine Guess(string boardId, string direction, double r, string session) =>
        new("guess", boardId, direction, "indice", ["a", "b"], ["a", "b"], r, 0, 1000,
            session, 1, new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc));

    private static ClueDecodeLine Decode(string boardId, string direction, int index, double? r) =>
        new("decode", boardId, direction, index, ["a", "b"], r, "0", r is null ? "outOfVocabulary" : null, 100);

    private static GuessingContents Humain(
        string session,
        IEnumerable<GuessingLine> lines,
        string benchHash = "aaaaaaaaaaaa",
        long seed = 20260807001,
        string runId = "run-a")
    {
        var list = lines.ToList();
        return new GuessingContents(
            new GuessingManifest("manifest", "eval/boards.dev.jsonl", benchHash, seed,
                runId, "eval/runs/run-a.jsonl", null, [], list.Count, 1,
                new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)),
            list);
    }

    private static DecodeContents Decodeur(params ClueDecodeLine[] lines) => new(null!, lines, []);

    /// <summary>Les trois séries appariées sur le même corpus, sinon les Δ ne se comparent pas.</summary>
    private static GuessingDispersionResult Lot(
        Func<int, double> h1, Func<int, double> h2, Func<int, double> d, int n = 40,
        int iterations = PairedComparison.DefaultBootstrapIterations)
    {
        var a = new List<GuessingLine>();
        var b = new List<GuessingLine>();
        var decodes = new List<ClueDecodeLine>();
        for (var i = 0; i < n; i++)
        {
            a.Add(Guess($"dev-{i:D3}", "Top", h1(i), "s-h1"));
            b.Add(Guess($"dev-{i:D3}", "Top", h2(i), "s-h2"));
            decodes.Add(Decode($"dev-{i:D3}", "Top", 0, d(i)));
        }

        return GuessingDispersion.Compare(
            Humain("s-h1", a), Humain("s-h2", b), Decodeur([.. decodes]), iterations);
    }

    // ── Appariement ─────────────────────────────────────────────────────────

    [Fact]
    public void Seules_les_directions_communes_aux_trois_series_sont_appariees()
    {
        var result = GuessingDispersion.Compare(
            Humain("s-h1", [Guess("dev-001", "Top", 1.0, "s-h1"), Guess("dev-002", "Top", 1.0, "s-h1")]),
            Humain("s-h2", [Guess("dev-001", "Top", 0.5, "s-h2"), Guess("dev-003", "Top", 1.0, "s-h2")]),
            Decodeur(Decode("dev-001", "Top", 0, 0.5), Decode("dev-002", "Top", 0, 1.0)),
            iterations: 0);

        Assert.Equal(1, result.PairedDirectionCount);
    }

    [Fact]
    public void Une_direction_sans_decodage_exploitable_sort_de_lappariement()
    {
        var result = GuessingDispersion.Compare(
            Humain("s-h1", [Guess("dev-001", "Top", 1.0, "s-h1"), Guess("dev-002", "Top", 1.0, "s-h1")]),
            Humain("s-h2", [Guess("dev-001", "Top", 0.5, "s-h2"), Guess("dev-002", "Top", 0.5, "s-h2")]),
            Decodeur(Decode("dev-001", "Top", 0, 0.5), Decode("dev-002", "Top", 0, null)),
            iterations: 0);

        Assert.Equal(1, result.PairedDirectionCount);
    }

    [Fact]
    public void Les_trois_deltas_portent_sur_le_meme_sous_ensemble()
    {
        var result = Lot(h1: _ => 1.0, h2: _ => 0.5, d: _ => 0.0, n: 10, iterations: 0);

        Assert.Equal(0.5, result.DeltaH1H2, 3);
        Assert.Equal(1.0, result.DeltaH1Decoder, 3);
        Assert.Equal(0.5, result.DeltaH2Decoder, 3);
        Assert.Equal(1.0, result.H1Recovery, 3);
        Assert.Equal(0.5, result.H2Recovery, 3);
        Assert.Equal(0.0, result.DecoderRecovery, 3);
    }

    [Fact]
    public void Le_r_du_decodeur_est_la_moyenne_de_ses_decodages()
    {
        var result = GuessingDispersion.Compare(
            Humain("s-h1", [Guess("dev-001", "Top", 1.0, "s-h1")]),
            Humain("s-h2", [Guess("dev-001", "Top", 1.0, "s-h2")]),
            Decodeur(
                Decode("dev-001", "Top", 0, 0.0),
                Decode("dev-001", "Top", 1, 0.5),
                Decode("dev-001", "Top", 2, 1.0)),
            iterations: 0);

        Assert.Equal(0.5, result.DecoderRecovery, 3);
    }

    [Fact]
    public void Sans_direction_commune_la_comparaison_est_refusee()
    {
        Assert.Throws<InvalidOperationException>(() => GuessingDispersion.Compare(
            Humain("s-h1", [Guess("dev-001", "Top", 1.0, "s-h1")]),
            Humain("s-h2", [Guess("dev-009", "Left", 1.0, "s-h2")]),
            Decodeur(Decode("dev-001", "Top", 0, 0.5)),
            iterations: 0));
    }

    // ── Gardes de montage ───────────────────────────────────────────────────

    /// <summary>
    /// Le piège nommé au pré-enregistrement : <c>--out</c> oublié, et l'on compare la séance D
    /// avec elle-même. Deux séances qui partagent un identifiant de session sont la même séance.
    /// </summary>
    [Fact]
    public void Deux_seances_partageant_une_session_sont_refusees()
    {
        var lines = new[] { Guess("dev-001", "Top", 1.0, "s-h1") };

        var ex = Assert.Throws<InvalidOperationException>(() => GuessingDispersion.Compare(
            Humain("s-h1", lines),
            Humain("s-h1", lines),
            Decodeur(Decode("dev-001", "Top", 0, 0.5)),
            iterations: 0));

        Assert.Contains("même séance", ex.Message);
    }

    [Theory]
    [InlineData("bbbbbbbbbbbb", 20260807001L, "run-a")]
    [InlineData("aaaaaaaaaaaa", 20260807002L, "run-a")]
    [InlineData("aaaaaaaaaaaa", 20260807001L, "run-b")]
    public void Un_montage_divergent_est_refuse(string benchHash, long seed, string runId)
    {
        Assert.Throws<MismatchedBenchException>(() => GuessingDispersion.Compare(
            Humain("s-h1", [Guess("dev-001", "Top", 1.0, "s-h1")]),
            Humain("s-h2", [Guess("dev-001", "Top", 1.0, "s-h2")], benchHash, seed, runId),
            Decodeur(Decode("dev-001", "Top", 0, 0.5)),
            iterations: 0));
    }

    // ── Les trois issues pré-enregistrées ───────────────────────────────────

    /// <summary>
    /// Dispersion humaine établie (IC de Δ(H1,H2) hors de zéro) et |Δ(H1,D)| ≤ |Δ(H1,H2)| :
    /// le décodeur tombe <i>dans</i> l'écart entre deux humains. C'est l'issue prédite.
    /// </summary>
    [Fact]
    public void Un_decodeur_plus_proche_de_h1_que_h2_ne_lest_tombe_dans_la_dispersion()
    {
        var result = Lot(h1: _ => 1.0, h2: _ => 0.0, d: _ => 1.0);

        Assert.Equal(GuessingDispersion.VerdictDansLaDispersion, result.Verdict);
        Assert.True(result.CriterionMet);
    }

    [Fact]
    public void Un_ecart_au_decodeur_superieur_a_lecart_entre_humains_sort_de_la_dispersion()
    {
        var result = Lot(h1: _ => 1.0, h2: _ => 0.5, d: _ => 0.0);

        Assert.Equal(GuessingDispersion.VerdictHorsDispersion, result.Verdict);
        Assert.False(result.CriterionMet);
    }

    /// <summary>
    /// Issue alternative déclarée d'avance : les deux humains ne divergent pas de façon établie,
    /// donc il n'y a pas de dispersion à laquelle comparer quoi que ce soit — la séance ne tranche
    /// pas et un troisième devineur est requis. Le résultat littéral du critère reste rapporté.
    /// </summary>
    [Fact]
    public void Une_dispersion_humaine_non_etablie_ne_tranche_pas()
    {
        var result = Lot(
            h1: i => i % 2 == 0 ? 1.0 : 0.0,
            h2: i => i % 2 == 0 ? 0.0 : 1.0,
            d: _ => 0.5);

        Assert.Equal(GuessingDispersion.VerdictNeTranchePas, result.Verdict);
        Assert.True(result.CiLowH1H2 < 0 && result.CiHighH1H2 > 0);
    }
}
