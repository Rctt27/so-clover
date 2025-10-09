namespace SoClover.Domain;

public sealed class Game
{
    public GameId Id { get; }
    public string Language { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    private readonly Dictionary<PlayerId, Player> _players = new();

    public IReadOnlyCollection<Player> Players => _players.Values;

    public Game(GameId id, string? language = null)
    {
        Id = id;
        Language = string.IsNullOrWhiteSpace(language) ? "Français" : language.Trim();
    }

    public void AddPlayer(Player player)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Cannot join after game start.");
        _players[player.Id] = player;
    }

    public void StartWritingPhase()
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Writing phase can only start from Lobby.");
        if (_players.Count == 0)
            throw new NotEnoughPlayersException(1, _players.Count);
        Phase = GamePhase.WritingClues;
    }

    public void SetClue(PlayerId playerId, Direction direction, string clueText)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Cannot set clues outside WritingClues phase.");
        var player = RequirePlayer(playerId);
        player.Board.SetClue(direction, ClueText.Create(clueText));
    }

    public void StartGuessingPhase()
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");
        Phase = GamePhase.Guessing;
    }

    public GuessResult Guess(PlayerId ownerId, Direction direction, string guessedWord)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Cannot guess outside Guessing phase.");
        var player = RequirePlayer(ownerId);
        var expected = player.Board.GetClueText(direction);
        var correct = string.Equals(expected, guessedWord?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (correct)
        {
            player.Board.MarkGuessed(direction);
            if (Players.All(p => p.Board.IsComplete()))
            {
                Phase = GamePhase.Completed;
            }
        }
        return new GuessResult(correct, expected);
    }

    private Player RequirePlayer(PlayerId playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            throw new PlayerNotFoundException(playerId);
        return player;
    }
}

public readonly record struct GuessResult(bool IsCorrect, string ExpectedWord);


