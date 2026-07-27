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
    public void Parse_without_verb_yields_empty_verb()
    {
        var args = Args.Parse([]);

        Assert.Equal(string.Empty, args.Verb);
    }
}
