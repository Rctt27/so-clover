using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Présentation <b>à plat, étiquetée</b> (décodeur clue v8). L'escalier v6/v7 a établi que grouper
/// les seize mots en quatre blocs porte <c>intra_card_rate</c> à 0,485 — deux fois et demie le
/// hasard — sans qu'aucun mot du prompt ne mentionne les cartes : la proximité typographique est
/// un attracteur massif. Cette présentation porte la même information de partition <b>sans créer
/// la moindre adjacence</b> : l'ordre reste exactement celui du mélange à plat de v4, chaque mot
/// portant son étiquette en ligne. C'est la seule forme où la mécanique d'élimination invoquée par
/// l'auteur peut être évaluée sans le confond de mise en page.
/// </summary>
public class LabeledCardPresentationTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> Cards =
    [
        new[] { "Chirurgien", "Enfant", "Île", "Forêt" },
        new[] { "Vague", "Miel", "Tambour", "Ciel" },
        new[] { "Route", "Plage", "Sable", "Rocher" },
        new[] { "Oiseau", "Montagne", "Vent", "Pont" },
    ];

    private static string Render(long seed) =>
        ShuffleSeed.RenderLabeled(
            ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), seed),
            ShuffleSeed.ShuffleByCard(Cards, seed));

    private static IReadOnlyList<string> Lines(long seed) =>
        Render(seed).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    // Le point de tout l'exercice : l'ordre de balayage de v4 est préservé au mot près, donc
    // aucune adjacence intra-carte n'est introduite.
    [Fact]
    public void Keeps_exactly_the_flat_shuffle_order_of_v4()
    {
        var flat = ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), 20260808);

        var rendered = Lines(20260808)
            .Select(l => l[2..l.LastIndexOf(" (", StringComparison.Ordinal)])
            .ToList();

        Assert.Equal(flat, rendered);
    }

    [Fact]
    public void Renders_the_sixteen_words_exactly_once()
    {
        Assert.Equal(16, Lines(11).Count);
        Assert.All(Cards.SelectMany(c => c), w => Assert.Contains($"- {w} (", Render(11)));
    }

    [Fact]
    public void Gives_the_same_label_to_words_of_one_card_and_distinct_labels_across_cards()
    {
        var labelOf = Lines(5).ToDictionary(
            l => l[2..l.LastIndexOf(" (", StringComparison.Ordinal)],
            l => l[(l.LastIndexOf(" (", StringComparison.Ordinal) + 2)..].TrimEnd(')'));

        var labelsPerCard = Cards.Select(card => card.Select(w => labelOf[w]).Distinct().ToList()).ToList();

        Assert.All(labelsPerCard, labels => Assert.Single(labels));
        Assert.Equal(4, labelsPerCard.Select(l => l[0]).Distinct().Count());
    }

    // L'étiquette dérive de l'ordre MÉLANGÉ des cartes : sans cela « carte A » désignerait toujours
    // la première carte du board et la géométrie fuiterait par l'étiquette.
    [Fact]
    public void Anonymises_the_labels_the_first_board_card_is_not_always_A()
    {
        var seen = new HashSet<string>();

        for (long seed = 0; seed < 20; seed++)
        {
            var line = Lines(seed).Single(l => l.StartsWith("- Chirurgien ", StringComparison.Ordinal));
            seen.Add(line[(line.LastIndexOf(" (", StringComparison.Ordinal) + 2)..].TrimEnd(')'));
        }

        Assert.True(seen.Count > 1, $"l'étiquette de la première carte n'a jamais bougé sur 20 graines : {string.Join(",", seen)}");
    }

    [Fact]
    public void Never_names_a_position_or_a_face()
    {
        var rendered = Render(3);

        foreach (var forbidden in new[] { "Top", "Bottom", "Left", "Right", "Haut", "Bas", "Gauche", "Droite" })
            Assert.DoesNotContain(forbidden, rendered);
    }

    [Fact]
    public void Is_deterministic_for_a_given_seed()
    {
        Assert.Equal(Render(20260808), Render(20260808));
    }

    // Les décodages déjà payés sous v2-v7 doivent rester reproductibles au bit près.
    [Fact]
    public void Does_not_disturb_the_flat_shuffle_used_by_earlier_prompt_versions()
    {
        var flat = ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), 42);

        Render(42);

        Assert.Equal(flat, ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), 42));
    }
}
