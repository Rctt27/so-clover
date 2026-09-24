using SoClover.Eval.Bench;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval.Tracing;

[Collection(TracingCollection.Name)]
public class DecodeTracingTests
{
    private static readonly BenchContents Bench = LangfuseFixtures.Bench();
    private static BenchBoard Board => Bench.Boards[0];

    private static string CluePrompt() => Path.Combine(AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");
    private static string BoardPrompt() => Path.Combine(AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-board.md");

    private static (ClueDecoder, BoardDecoder, FakeChatClient) Decoders()
    {
        var fake = new FakeChatClient();
        var chat = EvalLlmConfig.Instrument(fake);
        return (new ClueDecoder(chat, new FilePromptLoader(), CluePrompt(), "m", 0.3f, null, 512),
                new BoardDecoder(chat, new FilePromptLoader(), BoardPrompt(), "m", 0.3f, null, 1024),
                fake);
    }

    private static DecodeUnitContext Context(IReadOnlyDictionary<(string, string), string> validClues) => new(
        LangfuseFixtures.Run(), LangfuseFixtures.DecodeManifest(), "run.fp", "ds-1", "fp", 2,
        Bench.Manifest.BenchHash, validClues, new HashSet<(string, string, int)>());

    private static string Picked(IReadOnlyList<string> words) => $$"""{"picked":["{{words[0]}}","{{words[1]}}"]}""";

    private static void EnqueueAll(FakeChatClient fake)
    {
        foreach (var d in Board.Directions) { fake.Enqueue(Picked(d.ReferenceWords)); fake.Enqueue(Picked(d.ReferenceWords)); }
        fake.Enqueue("{\"assignment\":{" + string.Join(",", Board.Directions.Select(d =>
            $"\"{d.Direction}\":[\"{d.ReferenceWords[0]}\",\"{d.ReferenceWords[1]}\"]")) + "}}");
    }

    [Fact]
    public async Task A_board_emits_one_experiment_item_per_direction_with_its_decodes_and_a_board_trace()
    {
        var (clue, board, fake) = Decoders();
        var valid = Board.Directions.ToDictionary(d => (Board.BoardId, d.Direction), _ => "Indice");
        EnqueueAll(fake);

        using var session = TracingTestKit.Start(out var exported);
        var result = await DecodeBoardUnit.RunAsync(clue, board, Context(valid), Board, default);
        session.Checkpoint(Board.BoardId);

        Assert.Equal(8, result.ClueLines.Count);
        Assert.NotNull(result.BoardLine);
        Assert.Equal(4, result.Items.Count);

        var roots = exported.Where(a => a.DisplayName == "experiment-item").ToList();
        Assert.Equal(4, roots.Count);
        var top = roots.Single(r => TracingTestKit.Tag(r, "langfuse.experiment.item.id") == $"{Board.BoardId}-Top");
        Assert.Equal(OtlpIds.TraceId("run.fp", $"{Board.BoardId}-Top"), top.TraceId.ToHexString());
        Assert.Equal(OtlpIds.TraceId(LangfuseFixtures.Run().Manifest.RunId, $"{Board.BoardId}-Top"),
            TracingTestKit.Tag(top, EvalTracing.Metadata("generate_trace_id")));
        Assert.Equal(LangfuseFixtures.Run().Manifest.RunId, TracingTestKit.Tag(top, EvalTracing.SessionId));

        var decodes = exported.Where(a => a.DisplayName.StartsWith("decode-clue") && a.ParentSpanId == top.SpanId).ToList();
        Assert.Equal(2, decodes.Count);
        Assert.All(decodes, d => Assert.Equal(top.SpanId.ToHexString(), TracingTestKit.Tag(d, "langfuse.experiment.item.root_observation_id")));
        Assert.All(decodes, d => Assert.Single(exported, c => c.Source.Name == EvalTracing.ChatSourceName && c.ParentSpanId == d.SpanId));
        Assert.Equal(result.Items.Single(i => i.ItemId == $"{Board.BoardId}-Top").RootSpanId, top.SpanId.ToHexString());

        var boardTrace = Assert.Single(exported, a => a.DisplayName == "decode-board");
        Assert.Equal(OtlpIds.TraceId("run.fp", Board.BoardId), boardTrace.TraceId.ToHexString());
        Assert.Contains(Board.Directions[0].ReferenceWords[0], TracingTestKit.Tag(boardTrace, EvalTracing.Output));
    }

    [Fact]
    public async Task A_direction_fully_decoded_in_a_prior_session_yields_no_item_but_an_A1_direction_keeps_its_zero()
    {
        var (clue, board, fake) = Decoders();
        var valid = Board.Directions.Where(d => d.Direction != "Left")
            .ToDictionary(d => (Board.BoardId, d.Direction), _ => "Indice");
        foreach (var d in Board.Directions.Where(d => d.Direction is "Right" or "Bottom"))
        { fake.Enqueue(Picked(d.ReferenceWords)); fake.Enqueue(Picked(d.ReferenceWords)); }
        var ctx = Context(valid) with
        {
            AlreadyDecoded = new HashSet<(string, string, int)>
            {
                (Board.BoardId, "Top", 0),
                (Board.BoardId, "Top", 1),
            },
        };

        using var session = TracingTestKit.Start(out var exported);
        var result = await DecodeBoardUnit.RunAsync(clue, board, ctx, Board, default);
        session.Checkpoint(Board.BoardId);

        Assert.DoesNotContain(result.Items, i => i.ItemId == $"{Board.BoardId}-Top");
        var left = result.Items.Single(i => i.ItemId == $"{Board.BoardId}-Left");
        Assert.False(left.HasValidClue);
        Assert.Empty(left.Decodes);
        Assert.Equal(3, result.Items.Count); // Right, Bottom (decodes) + Left (A-1) ; Top exclu (M-8)
    }

    [Fact]
    public async Task A_direction_without_valid_clue_is_an_item_without_decodes_and_no_board_decode_runs()
    {
        var (clue, board, fake) = Decoders();
        var valid = Board.Directions.Where(d => d.Direction != "Left")
            .ToDictionary(d => (Board.BoardId, d.Direction), _ => "Indice");
        foreach (var d in Board.Directions.Where(d => d.Direction != "Left"))
        { fake.Enqueue(Picked(d.ReferenceWords)); fake.Enqueue(Picked(d.ReferenceWords)); }

        using var session = TracingTestKit.Start(out var exported);
        var result = await DecodeBoardUnit.RunAsync(clue, board, Context(valid), Board, default);
        session.Checkpoint(Board.BoardId);

        Assert.Null(result.BoardLine);
        var left = result.Items.Single(i => i.ItemId == $"{Board.BoardId}-Left");
        Assert.False(left.HasValidClue);
        Assert.Empty(left.Decodes);
        Assert.Equal(4, exported.Count(a => a.DisplayName == "experiment-item"));
        Assert.DoesNotContain(exported, a => a.DisplayName == "decode-board");
    }

    [Fact]
    public async Task Without_tracing_the_unit_returns_the_same_lines()
    {
        var (clue, board, fake) = Decoders();
        var valid = Board.Directions.ToDictionary(d => (Board.BoardId, d.Direction), _ => "Indice");
        EnqueueAll(fake);
        using var session = TracingSession.Off();

        var result = await DecodeBoardUnit.RunAsync(clue, board, Context(valid), Board, default);

        Assert.Equal(8, result.ClueLines.Count);
        Assert.NotNull(result.BoardLine);
        Assert.All(result.Items, i => Assert.Equal(string.Empty, i.RootSpanId));
    }

    [Fact]
    public async Task Already_decoded_indices_are_skipped()
    {
        var (clue, board, fake) = Decoders();
        var valid = new Dictionary<(string, string), string> { [(Board.BoardId, "Top")] = "Indice" };
        fake.Enqueue(Picked(Board.Directions[0].ReferenceWords));
        var ctx = Context(valid) with { AlreadyDecoded = new HashSet<(string, string, int)> { (Board.BoardId, "Top", 0) } };

        using var session = TracingTestKit.Start(out var exported);
        var result = await DecodeBoardUnit.RunAsync(clue, board, ctx, Board, default);
        session.Checkpoint(Board.BoardId);

        var line = Assert.Single(result.ClueLines);
        Assert.Equal(1, line.DecodeIndex);
        Assert.Equal(1, Assert.Single(result.Items.Single(i => i.ItemId == $"{Board.BoardId}-Top").Decodes).DecodeIndex);
    }

    [Fact]
    public async Task A_board_whose_board_decode_already_exists_does_not_rerun_it()
    {
        var (clue, board, fake) = Decoders();
        var valid = Board.Directions.ToDictionary(d => (Board.BoardId, d.Direction), _ => "Indice");
        foreach (var d in Board.Directions) { fake.Enqueue(Picked(d.ReferenceWords)); fake.Enqueue(Picked(d.ReferenceWords)); }
        var ctx = Context(valid) with { AlreadyBoardDecoded = new HashSet<string> { Board.BoardId } };

        using var session = TracingTestKit.Start(out var exported);
        var result = await DecodeBoardUnit.RunAsync(clue, board, ctx, Board, default);
        session.Checkpoint(Board.BoardId);

        Assert.Equal(8, result.ClueLines.Count);
        Assert.Null(result.BoardLine);
        Assert.DoesNotContain(exported, a => a.DisplayName == "decode-board");
    }
}
