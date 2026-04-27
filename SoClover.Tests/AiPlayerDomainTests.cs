using System.Text.Json;
using System.Text.Json.Serialization;
using SoClover.Domain;
using Xunit;

namespace SoClover.Tests;

public class AiPlayerDomainTests
{
    /// <summary>
    /// Crée des options JSON identiques à celles d'EfGameRepository.
    /// CRITICAL: toute divergence ici fausserait les tests de round-trip.
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return options;
    }

    [Fact]
    public void AIConfig_round_trip_JSON_preserves_model_and_temperature()
    {
        var config = new AIConfig("gpt-4o-mini", 0.7);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(config, options);
        var deserialized = JsonSerializer.Deserialize<AIConfig>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal("gpt-4o-mini", deserialized!.Model);
        Assert.Equal(0.7, deserialized.Temperature);
    }

    [Fact]
    public void Player_default_constructor_creates_human_player()
    {
        var player = new Player(PlayerId.New(), "Alice");

        Assert.False(player.IsAI);
        Assert.Null(player.AIConfig);
    }

    [Fact]
    public void Player_AI_constructor_sets_IsAI_and_AIConfig()
    {
        var config = new AIConfig("gpt-4o-mini", 0.7);
        var player = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: config);

        Assert.True(player.IsAI);
        Assert.NotNull(player.AIConfig);
        Assert.Equal("gpt-4o-mini", player.AIConfig!.Model);
        Assert.Equal(0.7, player.AIConfig.Temperature);
    }

    [Fact]
    public void Player_AI_round_trip_JSON_preserves_IsAI_and_AIConfig()
    {
        var config = new AIConfig("claude-haiku-4-5", 0.5);
        var original = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: config);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(original, options);
        var rehydrated = JsonSerializer.Deserialize<Player>(json, options);

        Assert.NotNull(rehydrated);
        Assert.True(rehydrated!.IsAI);
        Assert.NotNull(rehydrated.AIConfig);
        Assert.Equal("claude-haiku-4-5", rehydrated.AIConfig!.Model);
        Assert.Equal(0.5, rehydrated.AIConfig.Temperature);
        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(original.Name, rehydrated.Name);
    }

    [Fact]
    public void Player_human_round_trip_JSON_keeps_IsAI_false_and_AIConfig_null()
    {
        var original = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var options = CreateJsonOptions();

        var json = JsonSerializer.Serialize(original, options);
        var rehydrated = JsonSerializer.Deserialize<Player>(json, options);

        Assert.NotNull(rehydrated);
        Assert.False(rehydrated!.IsAI);
        Assert.Null(rehydrated.AIConfig);
    }

    [Fact]
    public void GuessingParticipants_excludes_AI_players()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(human);
        game.AddPlayer(bot);

        Assert.Single(game.GuessingParticipants);
        Assert.Contains(game.GuessingParticipants, p => p.Id == human.Id);
        Assert.DoesNotContain(game.GuessingParticipants, p => p.Id == bot.Id);
    }

    [Fact]
    public void GuessingParticipants_excludes_disconnected_humans()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice");
        var bob = new Player(PlayerId.New(), "Bob");
        game.AddPlayer(alice);
        game.AddPlayer(bob);
        bob.MarkDisconnected();

        Assert.Single(game.GuessingParticipants);
        Assert.Contains(game.GuessingParticipants, p => p.Id == alice.Id);
    }

    [Fact]
    public void GuessingParticipants_is_empty_when_only_AIs_and_disconnected_remain()
    {
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(admin);
        game.AddPlayer(bot);
        admin.MarkDisconnected();

        Assert.Empty(game.GuessingParticipants);
    }

    [Fact]
    public void BoardsToGuess_includes_AI_boards_when_submitted()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice");
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true, aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(human);
        game.AddPlayer(bot);

        Assert.Empty(game.BoardsToGuess);

        var now = DateTime.UtcNow;
        human.Board.MarkSubmitted(now);
        bot.Board.MarkSubmitted(now);

        Assert.Equal(2, game.BoardsToGuess.Count);
        Assert.Contains(game.BoardsToGuess, p => p.Id == human.Id);
        Assert.Contains(game.BoardsToGuess, p => p.Id == bot.Id);
    }

    [Fact]
    public void BoardsToGuess_excludes_non_submitted_boards()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice");
        var bob = new Player(PlayerId.New(), "Bob");

        game.AddPlayer(alice);
        game.AddPlayer(bob);

        alice.Board.MarkSubmitted(DateTime.UtcNow);

        Assert.Single(game.BoardsToGuess);
        Assert.Contains(game.BoardsToGuess, p => p.Id == alice.Id);
    }

    [Fact]
    public void BoardsToGuess_excludes_disconnected_players_even_if_submitted()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice");
        game.AddPlayer(alice);
        alice.Board.MarkSubmitted(DateTime.UtcNow);
        alice.MarkDisconnected();

        Assert.Empty(game.BoardsToGuess);
    }

    [Fact]
    public void MaxAIPlayersReachedException_exposes_currentCount_and_max_and_descriptive_message()
    {
        var ex = new MaxAIPlayersReachedException(currentCount: 4, max: 4);

        Assert.Equal(4, ex.CurrentCount);
        Assert.Equal(4, ex.Max);
        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void NoHumanGuesserException_is_a_DomainException_with_message()
    {
        var ex = new NoHumanGuesserException();

        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void LlmBudgetExhaustedException_exposes_gameId_and_max()
    {
        var gameId = GameId.New();
        var ex = new LlmBudgetExhaustedException(gameId, max: 100);

        Assert.Equal(gameId, ex.GameId);
        Assert.Equal(100, ex.Max);
        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public void UnsupportedAiLanguageException_exposes_language()
    {
        var ex = new UnsupportedAiLanguageException("Klingon");

        Assert.Equal("Klingon", ex.Language);
        Assert.IsAssignableFrom<DomainException>(ex);
        Assert.Contains("Klingon", ex.Message);
    }

    [Fact]
    public void AddAIPlayer_marks_player_as_AI_and_assigns_cursor_color()
    {
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        game.AddPlayer(admin);

        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));
        game.AddAIPlayer(bot, max: 4);

        Assert.Contains(game.Players, p => p.Id == bot.Id && p.IsAI);
    }

    [Fact]
    public void AddAIPlayer_throws_MaxAIPlayersReachedException_when_cap_reached()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(new Player(PlayerId.New(), "Admin", isAdmin: true));
        for (int i = 0; i < 4; i++)
        {
            game.AddAIPlayer(
                new Player(PlayerId.New(), $"Bot-{i}", isAdmin: false, isAI: true,
                    aiConfig: new AIConfig("gpt-4o-mini", 0.7)),
                max: 4);
        }

        var extra = new Player(PlayerId.New(), "Bot-extra", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        var ex = Assert.Throws<MaxAIPlayersReachedException>(
            () => game.AddAIPlayer(extra, max: 4));
        Assert.Equal(4, ex.CurrentCount);
        Assert.Equal(4, ex.Max);
    }

    private class DummyWordDictionary : IWordDictionary
    {
        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)Enumerable.Range(0, count).Select(i => $"Word{i}").ToList());
        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)new List<string> { "Word1", "Word2" });
    }

    [Fact]
    public void AddAIPlayer_throws_when_phase_is_not_Lobby()
    {
        var game = new Game(GameId.New());
        var admin = new Player(PlayerId.New(), "Admin", isAdmin: true);
        var human = new Player(PlayerId.New(), "Alice");
        game.AddPlayer(admin);
        game.AddPlayer(human);
        game.InitializeWordsPoolAsync(new DummyWordDictionary()).Wait();
        game.StartWritingPhase(DateTime.UtcNow, TimeSpan.FromMinutes(5));

        var bot = new Player(PlayerId.New(), "Bot-late", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        Assert.Throws<InvalidOperationInPhaseException>(
            () => game.AddAIPlayer(bot, max: 4));
    }

    [Fact]
    public void AddAIPlayer_throws_ArgumentException_when_player_is_not_AI()
    {
        var game = new Game(GameId.New());
        game.AddPlayer(new Player(PlayerId.New(), "Admin", isAdmin: true));
        var human = new Player(PlayerId.New(), "Alice"); // IsAI = false

        Assert.Throws<ArgumentException>(() => game.AddAIPlayer(human, max: 4));
    }

    [Fact]
    public void IsLastGuessingBoard_returns_true_when_completedBoardsCount_equals_boardsToGuessCount_minus_one()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bob = new Player(PlayerId.New(), "Bob");
        game.AddPlayer(alice);
        game.AddPlayer(bob);

        var now = DateTime.UtcNow;
        alice.Board.MarkSubmitted(now);
        bob.Board.MarkSubmitted(now);

        // 2 boards à deviner, 0 complétés → pas le dernier
        Assert.False(game.IsLastGuessingBoard());
    }

    [Fact]
    public void IsLastGuessingBoard_returns_false_when_no_boards_submitted()
    {
        var game = new Game(GameId.New());
        var alice = new Player(PlayerId.New(), "Alice", isAdmin: true);
        game.AddPlayer(alice);

        Assert.False(game.IsLastGuessingBoard());
    }

    [Fact]
    public void Player_deserialization_from_preEpic_JSON_snapshot_has_IsAI_false_and_AIConfig_null()
    {
        // Snapshot JSON représentatif d'un Player sérialisé AVANT l'ajout d'IsAI/AIConfig.
        // Les champs id/name/isAdmin/board/cursorColorIndex/isDisconnected sont présents ;
        // isAI et aiConfig sont volontairement absents.
        var legacyJson = """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "LegacyPlayer",
              "isAdmin": true,
              "board": {
                "topLeft": null,
                "topRight": null,
                "bottomRight": null,
                "bottomLeft": null,
                "topClue": null,
                "rightClue": null,
                "bottomClue": null,
                "leftClue": null,
                "isSubmitted": false,
                "submittedAtUtc": null,
                "guessedDirections": []
              },
              "cursorColorIndex": 3,
              "isDisconnected": false
            }
            """;
        var options = CreateJsonOptions();
        options.Converters.Add(new SoClover.Infrastructure.Persistence.PlayerIdJsonConverter());

        var player = JsonSerializer.Deserialize<Player>(legacyJson, options);

        Assert.NotNull(player);
        Assert.Equal("LegacyPlayer", player!.Name);
        Assert.True(player.IsAdmin);
        Assert.Equal(3, player.CursorColorIndex);
        Assert.False(player.IsDisconnected);

        Assert.False(player.IsAI);
        Assert.Null(player.AIConfig);
    }

    [Fact]
    public void MoveToNextGuessingBoard_cycles_on_BoardsToGuess_including_AI_boards()
    {
        var game = new Game(GameId.New());
        var human = new Player(PlayerId.New(), "Alice", isAdmin: true);
        var bot = new Player(PlayerId.New(), "Bot-1", isAdmin: false, isAI: true,
            aiConfig: new AIConfig("gpt-4o-mini", 0.7));

        game.AddPlayer(human);
        game.AddPlayer(bot);

        var now = DateTime.UtcNow;
        var card = new Card(CardId.New(), "a", "b", "c", "d");
        human.Board.Place(BoardPosition.TopLeft,     new OrientedCard(card));
        human.Board.Place(BoardPosition.TopRight,    new OrientedCard(card));
        human.Board.Place(BoardPosition.BottomRight, new OrientedCard(card));
        human.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(card));
        bot.Board.Place(BoardPosition.TopLeft,       new OrientedCard(card));
        bot.Board.Place(BoardPosition.TopRight,      new OrientedCard(card));
        bot.Board.Place(BoardPosition.BottomRight,   new OrientedCard(card));
        bot.Board.Place(BoardPosition.BottomLeft,    new OrientedCard(card));
        human.Board.MarkSubmitted(now);
        bot.Board.MarkSubmitted(now);

        game.InitializeWordsPoolAsync(new DummyWordDictionary()).Wait();
        game.StartWritingPhase(now, TimeSpan.FromMinutes(5));

        var rotations = new[] { Rotation.None, Rotation.None, Rotation.None, Rotation.None, Rotation.None };
        game.StartGuessingPhase(human.Id, card, rotations, now, TimeSpan.FromMinutes(1));

        Assert.False(game.IsLastGuessingBoard());

        game.MoveToNextGuessingBoard(card, rotations, now, TimeSpan.FromMinutes(1));
        Assert.Equal(GamePhase.Guessing, game.Phase);
        Assert.True(game.IsLastGuessingBoard());

        Assert.Contains(game.BoardsToGuess, p => p.Id == game.CurrentGuessingBoardOwner);

        game.MoveToNextGuessingBoard(card, rotations, now, TimeSpan.FromMinutes(1));
        Assert.Equal(GamePhase.Scoring, game.Phase);
    }
}
