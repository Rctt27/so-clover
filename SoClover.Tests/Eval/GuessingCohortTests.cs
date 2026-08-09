using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Règle d'agrégation pour K devineurs, <b>pré-enregistrée au registre le 2026-08-09 avant le
/// premier import</b>. Ces tests la figent ; ils ne l'arbitrent pas.
/// </summary>
public class GuessingCohortTests
{
    private static GuessingLine Guess(string boardId, double r, string session) =>
        new("guess", boardId, "Top", "indice", ["a", "b"], ["a", "b"], r, 0, 1000,
            session, 1, new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc));

    private static GuessingContents Humain(
        string session, IReadOnlyList<double> rs,
        string benchHash = "aaaaaaaaaaaa", long seed = 20260807001, string runId = "run-a")
    {
        var lines = rs.Select((r, i) => Guess($"dev-{i:D3}", r, session)).ToList();
        return new GuessingContents(
            new GuessingManifest("manifest", "eval/boards.dev.jsonl", benchHash, seed, runId,
                "eval/runs/run-a.jsonl", null, [], lines.Count, HumanFile.HarnessVersion,
                new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)),
            lines.AsReadOnly());
    }

    private static DecodeContents Decodeur(IReadOnlyList<double?> rs) => new(
        null!,
        rs.Select((r, i) => new ClueDecodeLine(
            "decode", $"dev-{i:D3}", "Top", 0, ["a", "b"], r, "0",
            r is null ? "outOfVocabulary" : null, 100)).ToList().AsReadOnly(),
        []);

    /// <summary>n valeurs constantes, décalées de <paramref name="offset"/> une direction sur deux.</summary>
    private static List<double> Serie(int n, double bas, double haut) =>
        Enumerable.Range(0, n).Select(i => i % 2 == 0 ? bas : haut).ToList();

    // ── Appariement et dénominateur ─────────────────────────────────────────

    /// <summary>
    /// « Un seul dénominateur » : l'intersection des K corpus ET du décodage. Une direction que
    /// l'un des devineurs n'a pas jouée sort pour <b>tout le monde</b>, sans quoi deux Δ de
    /// dénominateurs différents se compareraient.
    /// </summary>
    [Fact]
    public void Toutes_les_quantites_portent_sur_l_intersection_des_corpus()
    {
        var h1 = Humain("s-1", [1.0, 1.0, 1.0]);
        var h2 = Humain("e-2", [1.0, 1.0]);              // n'a pas joué dev-002
        var h3 = Humain("e-3", [1.0, 0.5, 1.0]);
        var decoded = Decodeur([1.0, 1.0, 1.0]);

        var result = GuessingCohort.Compare([h1, h2, h3], decoded, iterations: 0);

        Assert.Equal(2, result.PairedDirectionCount);
    }

    [Fact]
    public void Une_direction_sans_decodage_exploitable_sort_de_l_appariement()
    {
        var result = GuessingCohort.Compare(
            [Humain("s-1", [1.0, 1.0]), Humain("e-2", [1.0, 1.0])],
            Decodeur([1.0, null]),
            iterations: 0);

        Assert.Equal(1, result.PairedDirectionCount);
    }

    [Fact]
    public void Moins_de_deux_devineurs_est_refuse()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GuessingCohort.Compare(
            [Humain("s-1", [1.0])], Decodeur([1.0]), iterations: 0));

        Assert.Contains("au moins deux devineurs", ex.Message, StringComparison.Ordinal);
    }

    // ── S et E : des moyennes, jamais des maxima ────────────────────────────

    /// <summary>
    /// S est la moyenne des K(K−1)/2 écarts par paire, E la moyenne des K écarts au décodeur.
    /// Vérifié sur des séries construites pour que les deux se calculent de tête.
    /// </summary>
    [Fact]
    public void S_est_la_moyenne_des_paires_humaines_et_E_celle_des_ecarts_au_decodeur()
    {
        // R̄ : H1 = 1,0 · H2 = 0,5 · H3 = 0,0 · décodeur = 1,0
        var result = GuessingCohort.Compare(
            [Humain("s-1", [1.0, 1.0]), Humain("e-2", [0.5, 0.5]), Humain("e-3", [0.0, 0.0])],
            Decodeur([1.0, 1.0]),
            iterations: 0);

        // paires : |1,0−0,5| + |1,0−0,0| + |0,5−0,0| = 0,5 + 1,0 + 0,5, moyenne = 2,0/3
        Assert.Equal(2.0 / 3.0, result.HumanDispersion, 9);

        // écarts au décodeur : |1,0−1,0| + |0,5−1,0| + |0,0−1,0| = 0 + 0,5 + 1,0, moyenne = 0,5
        Assert.Equal(0.5, result.DecoderDistance, 9);
        Assert.True(result.CriterionMet);
    }

    /// <summary>
    /// La garde 7 rendue exécutable : ajouter un devineur <b>proche</b> des autres doit
    /// <b>resserrer</b> l'échelle, jamais l'élargir. Avec un maximum au lieu d'une moyenne, S
    /// n'aurait pas bougé et le critère serait devenu gratuitement plus facile.
    /// </summary>
    [Fact]
    public void Ajouter_un_devineur_proche_resserre_l_echelle_au_lieu_de_l_elargir()
    {
        var h1 = Humain("s-1", [1.0, 1.0]);
        var h2 = Humain("e-2", [0.0, 0.0]);
        var h3 = Humain("e-3", [1.0, 1.0]);
        var decoded = Decodeur([0.5, 0.5]);

        var deux = GuessingCohort.Compare([h1, h2], decoded, iterations: 0);
        var trois = GuessingCohort.Compare([h1, h2, h3], decoded, iterations: 0);

        Assert.Equal(1.0, deux.HumanDispersion, 9);            // une seule paire, écart 1,0
        Assert.Equal(2.0 / 3.0, trois.HumanDispersion, 9);     // (1,0 + 0,0 + 1,0) / 3
        Assert.True(trois.HumanDispersion < deux.HumanDispersion);
    }

    // ── Étage 1 : résolution, et correction de Bonferroni ───────────────────

    /// <summary>
    /// Le niveau est corrigé par le nombre de paires — m = K(K−1)/2 — parce que retenir la paire la
    /// plus séparée puis la tester est une sélection.
    /// </summary>
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 6)]
    public void Le_niveau_est_corrige_par_le_nombre_de_paires(int k, int paires)
    {
        var humains = Enumerable.Range(0, k)
            .Select(i => Humain(i == 0 ? "s-1" : $"e-{i}", Serie(20, 0.0, 1.0)))
            .ToList();

        var result = GuessingCohort.Compare(humains, Decodeur([.. Serie(20, 0.0, 1.0).Cast<double?>()]),
            iterations: 0);

        Assert.Equal(paires, result.HumanPairs.Count);
        Assert.Equal(0.05 / paires, result.CorrectedAlpha, 9);
    }

    /// <summary>
    /// Aucune paire séparée ⟹ pas d'échelle ⟹ « ne tranche pas », <b>même si le critère littéral
    /// est vérifié</b>. C'est l'étage de résolution du premier avenant, généralisé.
    /// </summary>
    [Fact]
    public void Sans_paire_separee_le_verdict_ne_tranche_pas_meme_critere_verifie()
    {
        // Trois devineurs identiques : aucun écart humain, donc aucune échelle.
        var identiques = new[] { "s-1", "e-2", "e-3" }
            .Select(s => Humain(s, Serie(20, 0.0, 1.0))).ToList();

        var result = GuessingCohort.Compare(
            identiques, Decodeur([.. Serie(20, 0.0, 1.0).Cast<double?>()]));

        Assert.False(result.DispersionEstablished);
        Assert.True(result.CriterionMet);
        Assert.Equal(GuessingCohort.VerdictNeTranchePas, result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("pas d'échelle", StringComparison.Ordinal));
    }

    /// <summary>Une paire franchement séparée établit la dispersion et laisse le critère trancher.</summary>
    [Fact]
    public void Une_paire_separee_etablit_la_dispersion_et_le_critere_tranche()
    {
        var h1 = Humain("s-1", [.. Enumerable.Repeat(1.0, 40)]);
        var h2 = Humain("e-2", [.. Enumerable.Repeat(0.0, 40)]);
        var h3 = Humain("e-3", [.. Enumerable.Repeat(1.0, 40)]);
        var decoded = Decodeur([.. Enumerable.Repeat((double?)1.0, 40)]);

        var result = GuessingCohort.Compare([h1, h2, h3], decoded);

        Assert.True(result.DispersionEstablished);
        // S = (1,0 + 0,0 + 1,0)/3 ≈ 0,667 ; E = (0 + 1,0 + 0)/3 ≈ 0,333
        Assert.True(result.CriterionMet);
        Assert.Equal(GuessingCohort.VerdictDansLaDispersion, result.Verdict);
    }

    /// <summary>Le décodeur plus loin des humains qu'ils ne le sont entre eux : hors dispersion.</summary>
    [Fact]
    public void Un_decodeur_plus_eloigne_que_les_humains_sort_de_la_dispersion()
    {
        var h1 = Humain("s-1", [.. Enumerable.Repeat(1.0, 40)]);
        var h2 = Humain("e-2", [.. Enumerable.Repeat(0.9, 40)]);
        var h3 = Humain("e-3", [.. Enumerable.Repeat(0.8, 40)]);
        var decoded = Decodeur([.. Enumerable.Repeat((double?)0.0, 40)]);

        var result = GuessingCohort.Compare([h1, h2, h3], decoded);

        Assert.True(result.DispersionEstablished);
        Assert.False(result.CriterionMet);
        Assert.Equal(GuessingCohort.VerdictHorsDispersion, result.Verdict);
    }

    // ── Diagnostic S_kit ────────────────────────────────────────────────────

    /// <summary>
    /// S_kit isole les paires kit ↔ kit — écart personne-à-personne pur, là où toute paire
    /// impliquant la séance servie mêle la personne et le transport. Diagnostic, jamais décisionnel.
    /// </summary>
    [Fact]
    public void S_kit_n_agrege_que_les_paires_entre_seances_hors_ligne()
    {
        var result = GuessingCohort.Compare(
            [Humain("s-1", [1.0, 1.0]), Humain("e-2", [0.0, 0.0]), Humain("e-3", [0.5, 0.5])],
            Decodeur([1.0, 1.0]),
            iterations: 0);

        Assert.Equal(1, result.KitOnlyPairCount);           // e-2 ↔ e-3 seulement
        Assert.Equal(0.5, result.KitOnlyDispersion!.Value, 9);
        Assert.NotEqual(result.HumanDispersion, result.KitOnlyDispersion!.Value);
    }

    /// <summary>Un seul devineur par kit : aucune paire kit ↔ kit, donc aucun diagnostic fabriqué.</summary>
    [Fact]
    public void Sans_deux_seances_hors_ligne_il_n_y_a_pas_de_S_kit()
    {
        var result = GuessingCohort.Compare(
            [Humain("s-1", [1.0, 1.0]), Humain("e-2", [0.0, 0.0])], Decodeur([1.0, 1.0]),
            iterations: 0);

        Assert.Null(result.KitOnlyDispersion);
        Assert.Equal(0, result.KitOnlyPairCount);
    }

    [Fact]
    public void Le_transport_de_chaque_seance_est_lu_dans_son_identifiant()
    {
        var result = GuessingCohort.Compare(
            [Humain("s-20260807135522", [1.0]), Humain("e-20260809140000-ab12", [0.0])],
            Decodeur([1.0]), iterations: 0);

        Assert.False(result.Humans[0].ViaKit);
        Assert.True(result.Humans[1].ViaKit);
    }

    // ── Gardes de montage ───────────────────────────────────────────────────

    /// <summary>
    /// La vérification est deux à deux sur l'ensemble : trois séances dont la <b>troisième</b>
    /// diverge doivent échouer, même si les deux premières concordent.
    /// </summary>
    [Theory]
    [InlineData("bench")]
    [InlineData("seed")]
    [InlineData("run")]
    public void Un_montage_divergent_sur_le_troisieme_corpus_est_refuse(string champ)
    {
        var h3 = champ switch
        {
            "bench" => Humain("e-3", [1.0], benchHash: "ffffffffffff"),
            "seed" => Humain("e-3", [1.0], seed: 20260807002),
            _ => Humain("e-3", [1.0], runId: "run-b"),
        };

        Assert.Throws<MismatchedBenchException>(() => GuessingCohort.Compare(
            [Humain("s-1", [1.0]), Humain("e-2", [1.0]), h3], Decodeur([1.0]), iterations: 0));
    }

    /// <summary>
    /// Deux fichiers partageant une session sont la même séance. La garde vaut sur toutes les
    /// paires, pas seulement contre le premier corpus.
    /// </summary>
    [Fact]
    public void Deux_corpus_partageant_une_session_sont_refuses()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GuessingCohort.Compare(
            [Humain("s-1", [1.0]), Humain("e-2", [1.0]), Humain("e-2", [1.0])],
            Decodeur([1.0]), iterations: 0));

        Assert.Contains("même séance", ex.Message, StringComparison.Ordinal);
    }
}
