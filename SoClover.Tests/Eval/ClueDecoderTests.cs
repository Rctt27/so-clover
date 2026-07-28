using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using Xunit;

namespace SoClover.Tests.Eval;

public class ClueDecoderTests
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
        Path.Combine(AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");

    private static ClueDecoder Build(FakeChatClient chat) =>
        new(chat, new FilePromptLoader(), PromptPath(), "decodeur-test", 0.3f, null, 512);

    private static string Picked(params string[] words) =>
        $$"""{"picked":["{{string.Join("\",\"", words)}}"]}""";

    [Fact]
    public void Exposes_the_decoder_prompt_version_independently_of_the_generator()
    {
        Assert.Equal(1, Build(new FakeChatClient()).PromptVersion);
    }

    [Fact]
    public async Task Scores_R_equals_1_when_both_reference_words_are_picked()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(reference[0], reference[1]));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(1.0, line.R);
        Assert.Null(line.DecodeFailureKind);
        Assert.Equal("decode", line.Kind);
        Assert.Equal("dev-001", line.BoardId);
        Assert.Equal("Top", line.Direction);
        Assert.Equal(0, line.DecodeIndex);
    }

    // La signature « Hôpital » : l'indice n'attrape qu'un des deux mots.
    [Fact]
    public async Task Scores_R_equals_0_5_when_a_single_reference_word_is_picked()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var distractor = BenchBoardMapper.AllWords(board).First(w => !reference.Contains(w));
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(reference[0], distractor));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(0.5, line.R);
    }

    [Fact]
    public async Task Scores_R_equals_0_when_neither_reference_word_is_picked()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var distractors = BenchBoardMapper.AllWords(board).Where(w => !reference.Contains(w)).Take(2).ToList();
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(distractors[0], distractors[1]));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(0.0, line.R);
    }

    [Fact]
    public async Task Matches_picked_words_case_and_accent_insensitively_but_stores_the_board_spelling()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(reference[0].ToUpperInvariant(), reference[1].ToLowerInvariant()));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(1.0, line.R);
        Assert.Equal(reference, line.Picked);
    }

    [Fact]
    public async Task Strips_json_fences_and_think_tags()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue($"<think>je réfléchis</think>```json\n{Picked(reference[0], reference[1])}\n```");

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(1.0, line.R);
    }

    // Sortie invalide : retentée UNE fois avec le même ordre de présentation.
    [Fact]
    public async Task Retries_once_on_an_out_of_vocabulary_answer_then_succeeds()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue(Picked("MotAbsentDuPlateau", reference[1]));
        chat.Enqueue(Picked(reference[0], reference[1]));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(2, chat.CallCount);
        Assert.Equal(1.0, line.R);
        Assert.Null(line.DecodeFailureKind);
    }

    [Fact]
    public async Task Gives_up_after_one_retry_and_records_outOfVocabulary()
    {
        var board = Board();
        var chat = new FakeChatClient();
        chat.Enqueue(Picked("Inconnu1", "Inconnu2"));
        chat.Enqueue(Picked("Inconnu3", "Inconnu4"));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal(2, chat.CallCount);
        Assert.Equal("outOfVocabulary", line.DecodeFailureKind);
        Assert.Null(line.R);
        Assert.Null(line.Picked);
    }

    [Fact]
    public async Task Records_tooFewWords_when_the_two_picks_are_identical()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(reference[0], reference[0]));
        chat.Enqueue(Picked(reference[0], reference[0]));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal("tooFewWords", line.DecodeFailureKind);
        Assert.Null(line.R);
    }

    [Fact]
    public async Task Records_unparseable_on_unreadable_json()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("je pense que ce sont Chirurgien et Enfant");
        chat.Enqueue("toujours pas du JSON");

        var line = await Build(chat).DecodeAsync(Board(), Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal("unparseable", line.DecodeFailureKind);
    }

    [Fact]
    public async Task Records_empty_when_the_model_returns_no_content()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("");
        chat.Enqueue("");

        var line = await Build(chat).DecodeAsync(Board(), Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal("empty", line.DecodeFailureKind);
    }

    [Fact]
    public async Task Records_transport_without_retrying_the_network()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new HttpRequestException("connexion refusée"));

        var line = await Build(chat).DecodeAsync(Board(), Direction.Top, "Hôpital", 0, BenchHash, default);

        Assert.Equal("transport", line.DecodeFailureKind);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task The_shuffle_seed_is_recorded_and_stable_for_a_given_triple()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var chat = new FakeChatClient();
        chat.Enqueue(Picked(reference[0], reference[1]));

        var line = await Build(chat).DecodeAsync(board, Direction.Top, "Hôpital", 2, BenchHash, default);

        Assert.Equal(
            ShuffleSeed.ForClue(BenchHash, "dev-001", 2).ToString(),
            line.ShuffleSeed);
    }

    // Contrat d'aveuglement : ni la paire de référence, ni l'explication, ni les candidats,
    // ni les autres indices ne doivent apparaître dans le prompt envoyé.
    [Fact]
    public async Task The_prompt_never_leaks_the_reference_pair_ordering()
    {
        var board = Board();
        var reference = BenchBoardMapper.ReferenceWords(board, Direction.Top);
        var capturing = new CapturingChatClient(Picked(reference[0], reference[1]));

        var decoder = new ClueDecoder(
            capturing, new FilePromptLoader(), PromptPath(), "m", 0.3f, null, 512);
        await decoder.DecodeAsync(board, Direction.Top, "Hôpital", 0, BenchHash, default);

        var prompt = capturing.LastUserPrompt!;
        var boardOrder = string.Join("\n", BenchBoardMapper.AllWords(board).Select(w => $"- {w}"));

        Assert.DoesNotContain(boardOrder, prompt);
        Assert.DoesNotContain("Top", prompt);
        Assert.Contains("Hôpital", prompt);
        Assert.All(BenchBoardMapper.AllWords(board), w => Assert.Contains(w, prompt));
    }

    /// <summary>Client de test qui capture le dernier prompt utilisateur envoyé.</summary>
    private sealed class CapturingChatClient : Microsoft.Extensions.AI.IChatClient
    {
        private readonly string _response;
        public CapturingChatClient(string response) => _response = response;
        public string? LastUserPrompt { get; private set; }

        public Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastUserPrompt = messages.Last().Text;
            return Task.FromResult(new Microsoft.Extensions.AI.ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(
                    Microsoft.Extensions.AI.ChatRole.Assistant, _response)));
        }

        public IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            Microsoft.Extensions.AI.ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
