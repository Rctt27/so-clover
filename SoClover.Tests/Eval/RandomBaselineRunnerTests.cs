using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Runner;
using Xunit;

namespace SoClover.Tests.Eval;

public class RandomBaselineRunnerTests
{
    private static readonly string[] Words =
    [
        "Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
        "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont",
        "Lampe", "Poire", "Maison", "Cagoule", "Pompier", "Guitare", "Nuage", "Fusée",
    ];

    private static BenchBoard Board() =>
        BenchGenerator.Generate(
            "dev", 20260726001, 1, Words, "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)).Boards[0];

    private static RandomBaselineRunner Build(long seed = 42) =>
        new(Words, new FrenchOffClueValidator(), seed);

    [Fact]
    public void Produces_one_terminal_attempt_per_direction()
    {
        var attempt = Build().RunDirection(Board(), Direction.Top);

        Assert.Equal("attempt", attempt.Kind);
        Assert.Equal("dev-001", attempt.BoardId);
        Assert.Equal("Top", attempt.Direction);
        Assert.Equal(0, attempt.Attempt);
        Assert.Null(attempt.FailureKind);
        Assert.Equal("random-baseline", attempt.EffectiveModel);
        Assert.Equal(0, attempt.LatencyMs);
        Assert.Null(attempt.PromptVersion);
    }

    // Le plancher doit mesurer le décodeur, pas le validateur : les indices tirés sont légaux.
    [Fact]
    public void Every_drawn_clue_passes_ClueAcceptance()
    {
        var board = Board();
        var runner = Build();
        var validator = new FrenchOffClueValidator();
        var clover = BenchBoardMapper.ToCloverBoard(board);

        foreach (var dir in BoardGeometry.AllDirections)
        {
            var attempt = runner.RunDirection(board, dir);

            Assert.True(attempt.Valid);
            Assert.NotNull(attempt.Clue);
            Assert.True(ClueAcceptance.Check(attempt.Clue!, dir, clover, validator).IsValid);
        }
    }

    [Fact]
    public void Never_draws_a_word_present_on_the_board()
    {
        var board = Board();
        var boardWords = BenchBoardMapper.AllWords(board).ToHashSet();
        var runner = Build();

        foreach (var dir in BoardGeometry.AllDirections)
        {
            var clue = runner.RunDirection(board, dir).Clue;
            Assert.NotNull(clue);
            Assert.DoesNotContain(clue, boardWords);
        }
    }

    [Fact]
    public void Is_reproducible_for_a_given_seed()
    {
        var board = Board();

        var first = BoardGeometry.AllDirections.Select(d => Build(7).RunDirection(board, d).Clue).ToList();
        var second = BoardGeometry.AllDirections.Select(d => Build(7).RunDirection(board, d).Clue).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_different_seed_yields_different_clues()
    {
        var board = Board();

        var a = Build(1).RunDirection(board, Direction.Top).Clue;
        var b = Build(2).RunDirection(board, Direction.Top).Clue;

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Carries_no_candidates_and_a_marker_explanation()
    {
        var attempt = Build().RunDirection(Board(), Direction.Top);

        Assert.Null(attempt.Candidates);
        Assert.Equal("random-baseline", attempt.Explanation);
    }

    // Un dictionnaire dont tous les mots sont rejetés doit échouer proprement, pas boucler.
    [Fact]
    public void Gives_up_with_a_failureKind_when_no_legal_word_can_be_drawn()
    {
        var board = Board();
        var boardWords = BenchBoardMapper.AllWords(board).ToList();
        var runner = new RandomBaselineRunner(boardWords, new FrenchOffClueValidator(), 1);

        var attempt = runner.RunDirection(board, Direction.Top);

        Assert.Equal("randomBaselineExhausted", attempt.FailureKind);
        Assert.Null(attempt.Clue);
        Assert.False(attempt.Valid);
    }
}
