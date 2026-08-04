using SoClover.Eval.Analysis;
using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

public class FailureTaxonomyTests
{
    private static readonly string[] Reference = ["Chirurgien", "Enfant"];

    private static ClueDecodeLine Decode(int index, params string[] picked) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: picked, R: picked.Count(Reference.Contains) / 2.0,
        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100);

    private static ClueDecodeLine Failed(int index) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: null, R: null, ShuffleSeed: "1", DecodeFailureKind: "unparseable", LatencyMs: 100);

    private static string Mode(params ClueDecodeLine[] decodes) =>
        FailureTaxonomy.LabelDirection(Reference, decodes).Mode;

    // M0 n'est pas un mode d'échec — mais il faut un dénominateur honnête.
    [Fact]
    public void M0_is_a_direction_that_succeeded()
    {
        Assert.Equal(FailureModes.M0, Mode(
            Decode(0, "Chirurgien", "Enfant"),
            Decode(1, "Chirurgien", "Enfant"),
            Decode(2, "Chirurgien", "Miel")));
    }

    [Fact]
    public void The_success_threshold_is_three_quarters()
    {
        Assert.Equal(0.75, FailureTaxonomy.SuccessThreshold);
    }

    // M2, la signature « Hôpital » : l'indice n'attrape qu'une face, TOUJOURS LA MÊME.
    [Fact]
    public void M2_needs_two_half_decodes_missing_the_same_face()
    {
        Assert.Equal(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Route"),
            Decode(2, "Chirurgien", "Vent")));
    }

    [Fact]
    public void Two_half_decodes_missing_different_faces_are_not_M2()
    {
        Assert.NotEqual(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route")));
    }

    // M3 : signature CONCENTRÉE — un même distracteur revient.
    [Fact]
    public void M3_is_a_single_distractor_chosen_in_at_least_two_decodes()
    {
        Assert.Equal(FailureModes.M3, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Miel", "Ciel")));
    }

    // M1 : signature DISPERSÉE — au moins 4 mots non-référence distincts, R̄ ≤ 0,5 mais NON NUL
    // (à R̄ = 0, c'est M4 qui l'emporte : voir D9).
    [Fact]
    public void M1_is_at_least_four_distinct_wrong_words_and_a_low_RBar()
    {
        Assert.Equal(FailureModes.M1, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Route", "Vent"),
            Decode(2, "Ciel", "Pont")));
    }

    // M4 : R̄ = 0 ET aucun mot commun d'un décodage à l'autre.
    [Fact]
    public void M4_is_zero_recovery_with_no_word_shared_between_decodes()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel"),
            Decode(2, "Pont", "Sable")));
    }

    // D5 : avec un seul décodage, « aucun mot commun » est vide de sens — l'intersection d'un
    // singleton avec lui-même n'est jamais vide. Sans cette garde, M4 absorberait les
    // directions mal décodées.
    [Fact]
    public void A_single_scored_decode_cannot_be_M4()
    {
        Assert.Equal(FailureModes.Unclassified, Mode(Decode(0, "Miel", "Route")));
    }

    // L'ORDRE DE PRIORITÉ, sur un item qui satisfait plusieurs signatures à la fois.
    [Fact]
    public void M2_wins_over_M3_when_both_signatures_hold()
    {
        // Deux décodages à R = 0,5 manquant « Enfant », ET « Miel » choisi deux fois.
        var mode = Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Miel"));

        Assert.Equal(FailureModes.M2, mode);
    }

    [Fact]
    public void M3_wins_over_M1_when_both_signatures_hold()
    {
        // « Miel » concentré (2 fois) ET 4 mots faux distincts.
        var mode = Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Ciel", "Pont"));

        Assert.Equal(FailureModes.M3, mode);
    }

    // D9 : M4 s'évalue AVANT M1. Sous l'ordre littéral du design (M1 puis M4), M4 serait
    // STRUCTURELLEMENT INATTEIGNABLE — R̄ = 0 implique 2 mots faux par décodage, et une
    // intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux distincts, donc M1.
    [Fact]
    public void M4_wins_over_M1_because_zero_recovery_is_the_stronger_signature()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel")));
    }

    [Fact]
    public void The_priority_order_is_exposed_and_documented()
    {
        Assert.Equal(
            [FailureModes.M2, FailureModes.M3, FailureModes.M4, FailureModes.M1],
            FailureTaxonomy.PriorityOrder);
    }

    // Une taxonomie qui classe 100 % des items est une taxonomie qui triche.
    [Fact]
    public void An_item_matching_no_signature_is_left_unclassified()
    {
        // R̄ = 0,667 : sous le seuil de réussite, au-dessus de 0,5 (donc pas M1), non nul (pas M4),
        // deux faces manquées différentes (pas M2), aucun distracteur répété (pas M3).
        Assert.Equal(FailureModes.Unclassified, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route"),
            Decode(2, "Chirurgien", "Enfant")));
    }

    // D6 : une direction sans aucun décodage exploitable n'est pas un mode d'échec sémantique.
    [Fact]
    public void A_direction_without_a_single_scored_decode_is_unclassified_and_counted_apart()
    {
        var (mode, rbar, scored) = FailureTaxonomy.LabelDirection(Reference, [Failed(0), Failed(1)]);

        Assert.Equal(FailureModes.Unclassified, mode);
        Assert.Equal(0.0, rbar);
        Assert.Equal(0, scored);
    }

    // M5 reste MANUEL, et c'est une décision, pas un oubli : le harnais n'embarque aucune
    // ressource de fréquence lexicale, et un proxy inventé donnerait une fausse rigueur.
    [Fact]
    public void M5_is_never_produced_automatically()
    {
        var everyShape = new[]
        {
            Mode(Decode(0, "Chirurgien", "Enfant")),
            Mode(Decode(0, "Chirurgien", "Miel"), Decode(1, "Chirurgien", "Route")),
            Mode(Decode(0, "Miel", "Route"), Decode(1, "Vent", "Ciel")),
        };

        Assert.DoesNotContain(FailureModes.M5, everyShape);
    }

    [Fact]
    public void M5_still_has_a_label_for_the_hand_written_sample()
    {
        Assert.False(string.IsNullOrWhiteSpace(FailureModes.Label(FailureModes.M5)));
    }
}
