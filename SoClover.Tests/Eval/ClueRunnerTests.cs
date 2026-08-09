using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Runner;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.AI;
using Xunit;

namespace SoClover.Tests.Eval;

public class ClueRunnerTests
{
    private static BenchBoard Board() =>
        BenchGenerator.Generate(
            "dev", 20260726001, 1,
            ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
             "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont"],
            "Français_OFF", "f.txt", "h",
            new DateTime(2026, 7, 27, 0, 0, 0, DateTimeKind.Utc)).Boards[0];

    private static ClueRunner Build(FakeChatClient chat, int maxAttempts = 3)
    {
        var opts = Options.Create(new LlmOptions
        {
            DefaultModel = "modele-test",
            DefaultTemperature = 1.0,
            ReasoningEnabled = false,
        });
        return new ClueRunner(
            new AiClueLlmCaller(chat, opts),
            new FrenchAiCluePromptProvider(),
            new FrenchOffClueValidator(),
            maxAttempts,
            "Français_OFF");
    }

    private static string Json(Direction dir, string clue, string? candidates = null) =>
        candidates is null
            ? $$"""{"direction":"{{dir}}","clueWord":"{{clue}}","explanation":"e"}"""
            : $$"""{"direction":"{{dir}}","clueWord":"{{clue}}","explanation":"e","candidates":[{{candidates}}]}""";

    private static async Task<List<RunAttempt>> Run(
        ClueRunner runner, BenchBoard board, Direction dir)
    {
        var attempts = new List<RunAttempt>();
        await foreach (var a in runner.RunDirectionAsync(board, dir, CancellationToken.None))
            attempts.Add(a);
        return attempts;
    }

    [Fact]
    public async Task A_valid_clue_on_the_first_attempt_yields_one_line_and_stops()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json(Direction.Top, "Hôpital", "\"Hôpital (fort, fort)\""));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        var attempt = Assert.Single(attempts);
        Assert.Equal("attempt", attempt.Kind);
        Assert.Equal("dev-001", attempt.BoardId);
        Assert.Equal("Top", attempt.Direction);
        Assert.Equal(0, attempt.Attempt);
        Assert.Equal("Hôpital", attempt.Clue);
        Assert.True(attempt.Valid);
        Assert.Empty(attempt.RejectionRules);
        Assert.Null(attempt.FailureKind);
        Assert.Equal(["Hôpital (fort, fort)"], attempt.Candidates);
        Assert.Equal("modele-test", attempt.EffectiveModel);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task An_invalid_clue_is_logged_then_retried_with_feedback()
    {
        var board = Board();
        var forbidden = board.Cards[0][0]; // mot du board → rejeté par R1

        var chat = new FakeChatClient();
        chat.Enqueue(Json(Direction.Top, forbidden));
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), board, Direction.Top);

        Assert.Equal(2, attempts.Count);
        Assert.False(attempts[0].Valid);
        Assert.Contains("ExactMatch", attempts[0].RejectionRules);
        Assert.Equal(0, attempts[0].Attempt);
        Assert.True(attempts[1].Valid);
        Assert.Equal(1, attempts[1].Attempt);
    }

    [Fact]
    public async Task Stops_after_maxAttempts_without_a_valid_clue()
    {
        var board = Board();
        var forbidden = board.Cards[0][0];

        var chat = new FakeChatClient();
        for (var i = 0; i < 3; i++) chat.Enqueue(Json(Direction.Top, forbidden));

        var attempts = await Run(Build(chat, maxAttempts: 3), board, Direction.Top);

        Assert.Equal(3, attempts.Count);
        Assert.All(attempts, a => Assert.False(a.Valid));
        Assert.Equal(3, chat.CallCount);
    }

    [Fact]
    public async Task An_empty_response_is_recorded_as_failureKind_empty()
    {
        var chat = new FakeChatClient();
        chat.EnqueueResponse(new ChatResponse(new ChatMessage(ChatRole.Assistant, ""))
        {
            FinishReason = ChatFinishReason.Length,
            Usage = new UsageDetails { InputTokenCount = 2100, OutputTokenCount = 4096 },
        });
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("empty", attempts[0].FailureKind);
        Assert.Null(attempts[0].Clue);
        Assert.False(attempts[0].Valid);
        Assert.Equal(2100, attempts[0].InputTokens);
        Assert.Equal(4096, attempts[0].OutputTokens);
        Assert.True(attempts[1].Valid);
    }

    [Fact]
    public async Task Unreadable_json_is_recorded_as_failureKind_unparseable()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("ceci n'est pas du JSON");
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("unparseable", attempts[0].FailureKind);
        Assert.Null(attempts[0].Clue);
        Assert.True(attempts[1].Valid);
    }

    [Fact]
    public async Task A_clue_for_another_direction_is_recorded_as_directionMismatch()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json(Direction.Left, "Hôpital"));
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("directionMismatch", attempts[0].FailureKind);
        Assert.True(attempts[1].Valid);
    }

    [Fact]
    public async Task A_timeout_is_recorded_as_failureKind_timeout_and_the_run_continues()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new OperationCanceledException());
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("timeout", attempts[0].FailureKind);
        Assert.True(attempts[1].Valid);
    }

    [Fact]
    public async Task A_transport_error_is_recorded_as_failureKind_transport()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new HttpRequestException("connexion refusée"));
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("transport", attempts[0].FailureKind);
        Assert.True(attempts[1].Valid);
    }

    // Le SDK du provider (System.ClientModel, côté OpenAI/LM Studio) n'expose PAS la connexion
    // refusée telle quelle : sa politique de retry l'enveloppe dans une AggregateException
    // « Retry failed after N tries ». Sans inspection de la chaîne d'inner exceptions, un
    // LM Studio arrêté faisait planter le run entier au premier board au lieu de consigner
    // failureKind:"transport" et de continuer — la règle A-1 n'était honorée qu'en test.
    [Fact]
    public async Task A_transport_error_wrapped_by_the_provider_sdk_is_still_failureKind_transport()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new AggregateException(
            "Retry failed after 4 tries.",
            new HttpRequestException("connexion refusée"),
            new HttpRequestException("connexion refusée")));
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("transport", attempts[0].FailureKind);
        Assert.True(attempts[1].Valid);
    }

    // Un défaut de programmation ne doit PAS être avalé en « transport » : il doit remonter,
    // sinon un run entier se remplit de faux échecs réseau et le chiffre mesuré ment.
    [Fact]
    public async Task An_unrelated_exception_is_not_swallowed_as_transport()
    {
        var chat = new FakeChatClient();
        chat.EnqueueException(new NotSupportedException("bug de programmation"));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => Run(Build(chat, maxAttempts: 1), Board(), Direction.Top));
    }

    [Fact]
    public async Task An_empty_clue_word_is_recorded_as_unparseable_rather_than_crashing()
    {
        var chat = new FakeChatClient();
        chat.Enqueue("""{"direction":"Top","clueWord":"   ","explanation":"e"}""");
        chat.Enqueue(Json(Direction.Top, "Hôpital"));

        var attempts = await Run(Build(chat), Board(), Direction.Top);

        Assert.Equal("unparseable", attempts[0].FailureKind);
        Assert.True(attempts[1].Valid);
    }

    [Fact]
    public async Task A_too_long_clue_is_rejected_by_the_length_cap_not_by_the_validator()
    {
        var chat = new FakeChatClient();
        chat.Enqueue(Json(Direction.Top, new string('z', Game.MaxClueLength + 1)));

        var attempts = await Run(Build(chat, maxAttempts: 1), Board(), Direction.Top);

        Assert.False(attempts[0].Valid);
        Assert.Contains("TooLong", attempts[0].RejectionRules);
    }

    // Règle A-1 : un board dont les 4 directions échouent est consigné, jamais retiré.
    [Fact]
    public async Task Every_direction_yields_at_least_one_line_even_when_all_fail()
    {
        var board = Board();
        var chat = new FakeChatClient();
        for (var i = 0; i < 4; i++) chat.EnqueueException(new HttpRequestException("down"));

        var all = new List<RunAttempt>();
        var runner = Build(chat, maxAttempts: 1);
        foreach (var dir in BoardGeometry.AllDirections)
            all.AddRange(await Run(runner, board, dir));

        Assert.Equal(4, all.Count);
        Assert.All(all, a => Assert.Equal("transport", a.FailureKind));
        Assert.Equal(
            BoardGeometry.AllDirections.Select(d => d.ToString()),
            all.Select(a => a.Direction));
    }
}
