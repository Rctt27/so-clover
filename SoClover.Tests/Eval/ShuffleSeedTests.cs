using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

public class ShuffleSeedTests
{
    private static readonly string[] Words =
        Enumerable.Range(1, 16).Select(i => $"Mot{i:D2}").ToArray();

    [Fact]
    public void ForClue_is_deterministic()
    {
        Assert.Equal(
            ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-007", 0),
            ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-007", 0));
    }

    [Fact]
    public void ForClue_varies_with_every_component()
    {
        var reference = ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-007", 0);

        Assert.NotEqual(reference, ShuffleSeed.ForClue("ffffffffffff", "dev-007", 0));
        Assert.NotEqual(reference, ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-008", 0));
        Assert.NotEqual(reference, ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-007", 1));
    }

    [Fact]
    public void ForBoard_differs_from_any_clue_seed_of_the_same_board()
    {
        var board = ShuffleSeed.ForBoard("a1b2c3d4e5f6", "dev-007");

        for (var i = 0; i < 3; i++)
            Assert.NotEqual(board, ShuffleSeed.ForClue("a1b2c3d4e5f6", "dev-007", i));
    }

    [Fact]
    public void Shuffle_is_a_permutation_of_the_input()
    {
        var shuffled = ShuffleSeed.Shuffle(Words, ShuffleSeed.ForClue("h", "dev-001", 0));

        Assert.Equal(Words.OrderBy(w => w), shuffled.OrderBy(w => w));
    }

    [Fact]
    public void Shuffle_does_not_mutate_its_input()
    {
        var input = Words.ToList();
        var snapshot = input.ToList();

        ShuffleSeed.Shuffle(input, 42);

        Assert.Equal(snapshot, input);
    }

    [Fact]
    public void Shuffle_is_reproducible_for_a_given_seed()
    {
        var seed = ShuffleSeed.ForClue("h", "dev-001", 2);

        Assert.Equal(ShuffleSeed.Shuffle(Words, seed), ShuffleSeed.Shuffle(Words, seed));
    }

    // Les 3 décodages diffèrent par l'ordre de présentation : le biais de position est
    // neutralisé gratuitement.
    [Fact]
    public void The_three_decode_indices_yield_three_different_orders()
    {
        var orders = Enumerable.Range(0, 3)
            .Select(i => string.Join(",", ShuffleSeed.Shuffle(Words, ShuffleSeed.ForClue("h", "dev-001", i))))
            .ToList();

        Assert.Equal(3, orders.Distinct().Count());
    }
}
