using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Comparaison de la séance D : R̄ humain contre R̄ décodeur, <b>appariée par direction</b>. Le
/// verdict suit la règle pré-enregistrée au registre le 2026-08-07 — il n'est pas choisi après
/// coup, et les trois issues sont donc figées dans ces tests.
/// </summary>
public class GuessingComparisonTests
{
    private static GuessingLine Guess(string boardId, string direction, double r) =>
        new("guess", boardId, direction, "indice", ["a", "b"], ["a", "b"], r, 0, 1000,
            "s-test", 1, new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));

    private static ClueDecodeLine Decode(string boardId, string direction, int index, double r) =>
        new("decode", boardId, direction, index, ["a", "b"], r, "0", null, 100);

    private static GuessingContents Humain(params GuessingLine[] lines) =>
        new(new GuessingManifest("manifest", "eval/boards.dev.jsonl", "aaaaaaaaaaaa", 1,
            "run-a", "eval/runs/run-a.jsonl", null, [], lines.Length, 1,
            new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc)), lines);

    private static DecodeContents Decodeur(params ClueDecodeLine[] lines) =>
        new(null!, lines, []);

    [Fact]
    public void Seules_les_directions_communes_sont_appariees()
    {
        var result = GuessingComparison.Compare(
            Humain(Guess("dev-001", "Top", 1.0), Guess("dev-002", "Top", 1.0)),
            Decodeur(Decode("dev-001", "Top", 0, 0.5)),
            iterations: 0);

        Assert.Equal(1, result.PairedDirectionCount);
    }

    [Fact]
    public void Le_r_du_decodeur_est_la_moyenne_de_ses_decodages()
    {
        var result = GuessingComparison.Compare(
            Humain(Guess("dev-001", "Top", 1.0)),
            Decodeur(
                Decode("dev-001", "Top", 0, 0.0),
                Decode("dev-001", "Top", 1, 0.5),
                Decode("dev-001", "Top", 2, 1.0)),
            iterations: 0);

        Assert.Equal(0.5, result.DecoderRecovery, 3);
        Assert.Equal(0.5, result.Delta, 3);
    }

    /// <summary>Un décodage sans <c>r</c> (échec de format) ne compte pas comme un zéro.</summary>
    [Fact]
    public void Un_decodage_sans_r_est_exclu_de_la_moyenne()
    {
        var result = GuessingComparison.Compare(
            Humain(Guess("dev-001", "Top", 1.0)),
            Decodeur(
                Decode("dev-001", "Top", 0, 1.0),
                new ClueDecodeLine("decode", "dev-001", "Top", 1, null, null, "0", "outOfVocabulary", 100)),
            iterations: 0);

        Assert.Equal(1.0, result.DecoderRecovery, 3);
    }

    [Fact]
    public void Une_direction_sans_aucun_decodage_exploitable_sort_de_lappariement()
    {
        var result = GuessingComparison.Compare(
            Humain(Guess("dev-001", "Top", 1.0), Guess("dev-002", "Top", 1.0)),
            Decodeur(
                Decode("dev-001", "Top", 0, 0.5),
                new ClueDecodeLine("decode", "dev-002", "Top", 0, null, null, "0", "outOfVocabulary", 100)),
            iterations: 0);

        Assert.Equal(1, result.PairedDirectionCount);
    }

    [Fact]
    public void Sans_direction_commune_la_comparaison_est_refusee()
    {
        Assert.Throws<InvalidOperationException>(() => GuessingComparison.Compare(
            Humain(Guess("dev-001", "Top", 1.0)),
            Decodeur(Decode("dev-009", "Left", 0, 0.5)),
            iterations: 0));
    }

    // ── Les trois issues pré-enregistrées ───────────────────────────────────

    [Fact]
    public void Un_ic_contenant_zero_dit_que_linstrument_est_valide_et_la_cible_en_cause()
    {
        var humain = new List<GuessingLine>();
        var decode = new List<ClueDecodeLine>();
        for (var i = 0; i < 40; i++)
        {
            humain.Add(Guess($"dev-{i:D3}", "Top", i % 2 == 0 ? 1.0 : 0.0));
            decode.Add(Decode($"dev-{i:D3}", "Top", 0, i % 2 == 0 ? 1.0 : 0.0));
        }

        var result = GuessingComparison.Compare(Humain([.. humain]), Decodeur([.. decode]));

        Assert.Equal(GuessingComparison.VerdictInstrumentValide, result.Verdict);
    }

    [Fact]
    public void Un_ic_entierement_positif_dit_que_le_decodeur_est_le_joueur_le_plus_faible()
    {
        var humain = new List<GuessingLine>();
        var decode = new List<ClueDecodeLine>();
        for (var i = 0; i < 40; i++)
        {
            humain.Add(Guess($"dev-{i:D3}", "Top", 1.0));
            decode.Add(Decode($"dev-{i:D3}", "Top", 0, 0.0));
        }

        var result = GuessingComparison.Compare(Humain([.. humain]), Decodeur([.. decode]));

        Assert.Equal(GuessingComparison.VerdictDecodeurPlusFaible, result.Verdict);
        Assert.True(result.CiLow > 0);
    }

    [Fact]
    public void Un_ic_entierement_negatif_renvoie_au_reexamen_du_montage()
    {
        var humain = new List<GuessingLine>();
        var decode = new List<ClueDecodeLine>();
        for (var i = 0; i < 40; i++)
        {
            humain.Add(Guess($"dev-{i:D3}", "Top", 0.0));
            decode.Add(Decode($"dev-{i:D3}", "Top", 0, 1.0));
        }

        var result = GuessingComparison.Compare(Humain([.. humain]), Decodeur([.. decode]));

        Assert.Equal(GuessingComparison.VerdictMontageAReexaminer, result.Verdict);
    }
}
