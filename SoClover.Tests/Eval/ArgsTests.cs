using SoClover.Eval.Cli;
using Xunit;

namespace SoClover.Tests.Eval;

public class ArgsTests
{
    [Fact]
    public void Parse_extracts_verb_and_named_values()
    {
        var args = Args.Parse(["generate", "--bench", "eval/boards.dev.jsonl", "--notes", "thinking OFF"]);

        Assert.Equal("generate", args.Verb);
        Assert.Equal("eval/boards.dev.jsonl", args.Require("bench"));
        Assert.Equal("thinking OFF", args.Require("notes"));
    }

    [Fact]
    public void Parse_treats_valueless_flag_as_boolean()
    {
        var args = Args.Parse(["generate", "--force", "--bench", "b.jsonl"]);

        Assert.True(args.Has("force"));
        Assert.False(args.Has("random-baseline"));
        Assert.Equal("b.jsonl", args.Require("bench"));
    }

    [Fact]
    public void Require_throws_when_missing()
    {
        var args = Args.Parse(["score"]);

        var ex = Assert.Throws<ArgumentException>(() => args.Require("run"));
        Assert.Contains("--run", ex.Message);
    }

    [Fact]
    public void GetInt_falls_back_when_absent()
    {
        var args = Args.Parse(["decode", "--decodes", "5"]);

        Assert.Equal(5, args.GetInt("decodes", 3));
        Assert.Equal(3, args.GetInt("absent", 3));
    }

    [Fact]
    public void GetInt_throws_when_present_but_not_parsable()
    {
        var args = Args.Parse(["decode", "--decodes-per-clue", "x"]);

        var ex = Assert.Throws<ArgumentException>(() => args.GetInt("decodes-per-clue", 3));
        Assert.Contains("--decodes-per-clue", ex.Message);
        Assert.Contains("x", ex.Message);
    }

    [Fact]
    public void GetLong_falls_back_when_absent()
    {
        var args = Args.Parse(["bench", "--seed", "20260726001"]);

        Assert.Equal(20260726001L, args.GetLong("seed", 0));
        Assert.Equal(0L, args.GetLong("absent", 0));
    }

    [Fact]
    public void GetLong_throws_when_present_but_not_parsable()
    {
        var args = Args.Parse(["bench", "--seed", "abc"]);

        var ex = Assert.Throws<ArgumentException>(() => args.GetLong("seed", 0));
        Assert.Contains("--seed", ex.Message);
        Assert.Contains("abc", ex.Message);
    }

    [Fact]
    public void Parse_without_verb_yields_empty_verb()
    {
        var args = Args.Parse([]);

        Assert.Equal(string.Empty, args.Verb);
    }

    /// <summary>
    /// Un drapeau répété désigne plusieurs valeurs — c'est ce dont la règle d'agrégation a besoin
    /// pour recevoir K corpus de devineurs. <c>Get</c> continue de rendre la dernière : aucun verbe
    /// existant ne change de comportement.
    /// </summary>
    [Fact]
    public void GetAll_collects_repeated_flags_in_command_line_order()
    {
        var args = Args.Parse(
            ["guess-report", "--guessing", "a.jsonl", "--guessing", "b.jsonl", "--guessing", "c.jsonl"]);

        Assert.Equal(["a.jsonl", "b.jsonl", "c.jsonl"], args.GetAll("guessing"));
        Assert.Equal("c.jsonl", args.Get("guessing"));
    }

    [Fact]
    public void GetAll_yields_nothing_for_an_absent_flag()
    {
        Assert.Empty(Args.Parse(["guess-report"]).GetAll("guessing"));
    }

    /// <summary>Un drapeau nu n'apporte aucune valeur : il ne doit pas se compter comme un corpus.</summary>
    [Fact]
    public void GetAll_ignores_valueless_occurrences()
    {
        var args = Args.Parse(["guess-report", "--guessing", "--guessing", "b.jsonl"]);

        Assert.Equal(["b.jsonl"], args.GetAll("guessing"));
    }
}
