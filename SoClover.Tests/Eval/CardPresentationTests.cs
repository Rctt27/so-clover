using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Présentation des seize mots <b>groupés par carte</b> (décodeur clue v5). La structure en
/// cartes est une règle du jeu, pas une fuite : dans une partie réelle le devineur a les
/// quatre cartes physiques en main. Ce qui reste caché, c'est la position de chaque carte sur
/// le plateau et la face que porte chaque mot — d'où le double mélange.
/// </summary>
public class CardPresentationTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> Cards =
    [
        new[] { "Chirurgien", "Enfant", "Île", "Forêt" },
        new[] { "Vague", "Miel", "Tambour", "Ciel" },
        new[] { "Route", "Plage", "Sable", "Rocher" },
        new[] { "Oiseau", "Montagne", "Vent", "Pont" },
    ];

    [Fact]
    public void Keeps_the_four_cards_and_their_four_words_each()
    {
        var presented = ShuffleSeed.ShuffleByCard(Cards, 42);

        Assert.Equal(4, presented.Count);
        Assert.All(presented, card => Assert.Equal(4, card.Count));
    }

    [Fact]
    public void Preserves_the_partition_each_group_is_a_permutation_of_one_original_card()
    {
        var presented = ShuffleSeed.ShuffleByCard(Cards, 7);

        Assert.All(presented, card =>
            Assert.Single(Cards.Where(original => original.OrderBy(w => w).SequenceEqual(card.OrderBy(w => w)))));
    }

    [Fact]
    public void Keeps_the_sixteen_words_exactly_once()
    {
        var presented = ShuffleSeed.ShuffleByCard(Cards, 99);

        Assert.Equal(
            Cards.SelectMany(c => c).OrderBy(w => w, StringComparer.Ordinal),
            presented.SelectMany(c => c).OrderBy(w => w, StringComparer.Ordinal));
    }

    [Fact]
    public void Is_deterministic_for_a_given_seed()
    {
        Assert.Equal(ShuffleSeed.ShuffleByCard(Cards, 20260807), ShuffleSeed.ShuffleByCard(Cards, 20260807));
    }

    // Sans quoi les décodages d'un même indice partageraient l'ordre de présentation et le
    // biais de position cesserait d'être neutralisé.
    [Fact]
    public void Differs_between_seeds()
    {
        var a = ShuffleSeed.ShuffleByCard(Cards, 1).SelectMany(c => c);
        var b = ShuffleSeed.ShuffleByCard(Cards, 2).SelectMany(c => c);

        Assert.NotEqual(a, b);
    }

    // L'ordre des cartes ET l'ordre interne bougent : ni la position sur le plateau
    // (BoardPosition) ni la face (Direction) ne se lisent dans la présentation.
    [Fact]
    public void Shuffles_both_the_card_order_and_the_words_inside_each_card()
    {
        var cardOrderMoved = false;
        var wordOrderMoved = false;

        for (var seed = 0; seed < 20; seed++)
        {
            var presented = ShuffleSeed.ShuffleByCard(Cards, seed);
            if (!presented[0].OrderBy(w => w).SequenceEqual(Cards[0].OrderBy(w => w)))
                cardOrderMoved = true;
            if (presented.Any(card =>
                    Cards.Any(original => original.SequenceEqual(card)) is false))
                wordOrderMoved = true;
        }

        Assert.True(cardOrderMoved, "l'ordre des cartes n'a jamais bougé sur 20 graines");
        Assert.True(wordOrderMoved, "l'ordre interne des cartes n'a jamais bougé sur 20 graines");
    }

    // Le mélange à plat de v4 doit rester identique au bit près : les décodages déjà payés
    // sous v2/v3/v4 restent reproductibles.
    [Fact]
    public void Does_not_disturb_the_flat_shuffle_used_by_earlier_prompt_versions()
    {
        var flat = ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), 20260807);

        ShuffleSeed.ShuffleByCard(Cards, 20260807);

        Assert.Equal(flat, ShuffleSeed.Shuffle(Cards.SelectMany(c => c).ToList(), 20260807));
    }

    [Fact]
    public void Renders_anonymous_card_labels_that_never_name_a_position_or_a_face()
    {
        var rendered = ShuffleSeed.RenderByCard(ShuffleSeed.ShuffleByCard(Cards, 3));

        Assert.Contains("Carte 1", rendered);
        Assert.Contains("Carte 4", rendered);
        foreach (var forbidden in new[] { "Top", "Bottom", "Left", "Right", "Haut", "Bas", "Gauche", "Droite" })
            Assert.DoesNotContain(forbidden, rendered);
        Assert.All(Cards.SelectMany(c => c), w => Assert.Contains($"- {w}", rendered));
    }
}
