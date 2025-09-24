namespace SoClover.Domain;

public sealed class Game
{
    public GameId Id { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    private readonly Dictionary<PlayerId, Player> _players = new();

    public IReadOnlyCollection<Player> Players => _players.Values;

    public Game(GameId id)
    {
        Id = id;
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
            throw new DomainException("At least one player is required.");
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
        var visible = player.Board.GetVisibleWord(direction);
        if (visible is null)
            throw new InvalidGuessException("No card placed for that direction.");
        var correct = string.Equals(visible, guessedWord?.Trim(), StringComparison.OrdinalIgnoreCase);
        return new GuessResult(correct, visible);
    }

    private Player RequirePlayer(PlayerId playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            throw new DomainException("Player not found.");
        return player;
    }
}

public readonly record struct GuessResult(bool IsCorrect, string ExpectedWord);


