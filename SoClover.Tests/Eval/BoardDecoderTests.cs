using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using Xunit;

namespace SoClover.Tests.Eval;

public class BoardDecoderTests
{
    private const string BenchHash = "a1b2c3d4e5f6";

    private static readonly string[] Words =
    [
        "Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
        "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont",
    ];

    private static BenchBoard Board() =>
        BenchGenerator.Generate(
            "dev", 20260726001, 1, Words, "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)).Boards[0];

    private static string PromptPath() =>
        Path.Combine(AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-board.md");

    private static BoardDecoder Build(FakeChatClient chat) =>
        new(chat, new FilePromptLoader(), PromptPath(), "decodeur-test", 0.3f, null, 1024);

    private static IReadOnlyDictionary<Direction, string> Clues() =>
        BoardGeometry.AllDirections.ToDictionary(d => d, d => $"indice-{d}");

    private static string Assignment(BenchBoard board, Func<Direction, IReadOnlyList<string>> pick)
    {
        var parts = BoardGeometry.AllDirections.Select(d =>
            $"\"{d}\":[\"{string.Join("\",\"", pick(d))}\"]");
        return "{\"assignment\":{" + string.Join(",", parts) + "}}";
    }

    [Fact]
    public void Exposes_its_own_prompt_version()
    {
        Assert.Equal(1, Build(new FakeChatClient()).PromptVersion);
    }

    [Fact]
    public async Task A_perfect_assignment_scores_1_and_is_solved()
    {
        var board = Board();
        var chat = new FakeChatClient();
        chat.Enqueue(Assignment(board, d => BenchBoardMapper.ReferenceWords(board, d)));

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal("boardDecode", line.Kind);
        Assert.Equal("dev-001", line.BoardId);
        Assert.Equal(1.0, line.BoardPositions);
        Assert.True(line.BoardSolved);
        Assert.Null(line.DecodeFailureKind);
        Assert.Equal(4, line.Assignment!.Count);
    }

    // Crédit sommé sur les 4 directions, divisé par 8 — cohérent avec le R de N2.
    [Fact]
    public async Task A_partial_assignment_scores_credit_over_eight_slots()
    {
        var board = Board();
        var all = BenchBoardMapper.AllWords(board);
        var chat = new FakeChatClient();

        // Top parfait (2/2), Right à moitié (1/2), Bottom et Left nuls (0/2 chacun) → 3/8.
        chat.Enqueue(Assignment(board, d => d switch
        {
            Direction.Top => BenchBoardMapper.ReferenceWords(board, Direction.Top),
            Direction.Right =>
            [
                BenchBoardMapper.ReferenceWords(board, Direction.Right)[0],
                all.First(w => !BenchBoardMapper.ReferenceWords(board, Direction.Right).Contains(w)
                            && !BenchBoardMapper.ReferenceWords(board, Direction.Top).Contains(w)),
            ],
            _ => WrongPair(board, d),
        }));

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal(3.0 / 8.0, line.BoardPositions);
        Assert.False(line.BoardSolved);
    }

    [Fact]
    public async Task An_entirely_wrong_assignment_scores_zero()
    {
        var board = Board();
        var chat = new FakeChatClient();
        chat.Enqueue(Assignment(board, d => WrongPair(board, d)));

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal(0.0, line.BoardPositions);
        Assert.False(line.BoardSolved);
    }

    [Fact]
    public async Task Records_unparseable_when_a_direction_key_is_missing()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var partial = $$$"""{"assignment":{"Top":["{{{reference[0]}}}","{{{reference[1]}}}"]}}""";
        var chat = new FakeChatClient();
        chat.Enqueue(partial);
        chat.Enqueue(partial);

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal("unparseable", line.DecodeFailureKind);
        Assert.Null(line.BoardPositions);
        Assert.Null(line.BoardSolved);
    }

    [Fact]
    public async Task Records_outOfVocabulary_when_a_word_is_not_on_the_board()
    {
        var board = Board();
        var bogus = Assignment(board, _ => ["MotAbsent1", "MotAbsent2"]);
        var chat = new FakeChatClient();
        chat.Enqueue(bogus);
        chat.Enqueue(bogus);

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal("outOfVocabulary", line.DecodeFailureKind);
    }

    [Fact]
    public async Task Retries_once_before_giving_up()
    {
        var board = Board();
        var chat = new FakeChatClient();
        chat.Enqueue("pas du JSON");
        chat.Enqueue(Assignment(board, d => BenchBoardMapper.ReferenceWords(board, d)));

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal(2, chat.CallCount);
        Assert.Equal(1.0, line.BoardPositions);
    }

    [Fact]
    public async Task Uses_the_board_level_shuffle_seed()
    {
        var board = Board();
        var chat = new FakeChatClient();
        chat.Enqueue(Assignment(board, d => BenchBoardMapper.ReferenceWords(board, d)));

        var line = await Build(chat).DecodeAsync(board, Clues(), BenchHash, default);

        Assert.Equal(ShuffleSeed.ForBoard(BenchHash, "dev-001").ToString(), line.ShuffleSeed);
    }

    [Fact]
    public async Task Records_transport_without_retrying_the_network()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new HttpRequestException("down"));

        var line = await Build(chat).DecodeAsync(Board(), Clues(), BenchHash, default);

        Assert.Equal("transport", line.DecodeFailureKind);
        Assert.Equal(1, chat.CallCount);
    }

    private static IReadOnlyList<string> WrongPair(BenchBoard board, Direction direction)
    {
        var reference = BenchBoardMapper.ReferenceWords(board, direction);
        return BenchBoardMapper.AllWords(board)
            .Where(w => !reference.Contains(w))
            .Take(2)
            .ToList();
    }
}
