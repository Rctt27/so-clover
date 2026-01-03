using System.Text.Json.Serialization;

namespace SoClover.Domain;

public sealed class Game
{
    public GameId Id { get; }
    // With EF snapshot storage, we need to rehydrate properties with private setters.
    // System.Text.Json ignores private setters unless [JsonInclude] is present.
    [JsonInclude]
    [JsonPropertyName("language")]
    public string Language { get; private set; }

    [JsonInclude]
    [JsonPropertyName("phase")]
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    // Persist and rehydrate AdminPlayerId through JSON snapshots stored by EF
    // Without [JsonInclude], System.Text.Json would ignore the private setter during deserialization,
    // leaving AdminPlayerId null after reloading from the database and causing authorization to fail.
    [JsonInclude]
    [JsonPropertyName("adminPlayerId")]
    public PlayerId? AdminPlayerId { get; private set; }

    [JsonInclude]
    [JsonPropertyName("phaseEndsAtUtc")]
    public DateTime? PhaseEndsAtUtc { get; private set; }

    // Per-game overrides (seconds). When null, defaults from configuration are used.
    [JsonInclude]
    [JsonPropertyName("cluesDurationSecondsOverride")]
    public int? CluesDurationSecondsOverride { get; private set; }

    [JsonInclude]
    [JsonPropertyName("guessDurationSecondsOverride")]
    public int? GuessDurationSecondsOverride { get; private set; }

    private readonly Dictionary<PlayerId, Player> _players = new();
    private WordsPool? _wordsPool;

    // Guessing phase state
    [JsonInclude]
    [JsonPropertyName("currentGuessingBoardOwner")]
    public PlayerId? CurrentGuessingBoardOwner { get; private set; }

    [JsonInclude]
    [JsonPropertyName("outsideCards")]
    public List<OrientedCard> OutsideCards { get; private set; } = new();

    [JsonInclude]
    [JsonPropertyName("guessedCardPositions")]
    public Dictionary<BoardPosition, OrientedCard?> GuessedCardPositions { get; private set; } = new()
    {
        { BoardPosition.TopLeft, null },
        { BoardPosition.TopRight, null },
        { BoardPosition.BottomRight, null },
        { BoardPosition.BottomLeft, null }
    };
    
    [JsonInclude]
    [JsonPropertyName("remainingAttempts")]
    public int RemainingAttempts { get; private set; } = 3;

    [JsonInclude]
    [JsonPropertyName("correctlyPlacedPositions")]
    public HashSet<BoardPosition> CorrectlyPlacedPositions { get; private set; } = new();

    [JsonInclude]
    [JsonPropertyName("completedBoardsCount")]
    public int CompletedBoardsCount { get; private set; } = 0;

    // Scoring tracking
    [JsonInclude]
    [JsonPropertyName("currentBoardStartTime")]
    private DateTime _currentBoardStartTime;
    
    [JsonInclude]
    [JsonPropertyName("currentBoardAttempts")]
    private int _currentBoardAttempts = 0;
    
    private readonly Dictionary<PlayerId, BoardResult> _boardResults = new();

    [JsonIgnore]
    public IReadOnlyCollection<Player> Players => _players.Values;

    // Persistence bridge: expose players for JSON (de)serialization when using EF snapshot storage.
    // We keep the domain API read-only (via Players) and use this property only for persistence.
    // Note: requires custom JSON converters for PlayerId as dictionary keys (already added in EfGameRepository).
    [JsonInclude]
    [JsonPropertyName("players")] // store under a stable name; distinct from [JsonIgnore]d public Players
    public Dictionary<PlayerId, Player> PlayersPersistence
    {
        // Return a copy to avoid exposing the internal mutable dictionary to application code
        get => _players.ToDictionary(kv => kv.Key, kv => kv.Value);
        // Private setter is honored by System.Text.Json when [JsonInclude] is present
        private set
        {
            _players.Clear();
            if (value == null) return;
            foreach (var kv in value)
            {
                _players[kv.Key] = kv.Value;
            }
        }
    }
    [JsonIgnore]
    public IReadOnlyDictionary<PlayerId, BoardResult> BoardResults => _boardResults;

    // Persistence bridge for scoring results. EF stores the Game as a JSON snapshot; we must
    // expose a serializable view with a private setter so System.Text.Json can rehydrate it.
    // Requires PlayerId dictionary key converter (registered in EfGameRepository).
    [JsonInclude]
    [JsonPropertyName("boardResults")]
    public Dictionary<PlayerId, BoardResult> BoardResultsPersistence
    {
        get => _boardResults.ToDictionary(kv => kv.Key, kv => kv.Value);
        private set
        {
            _boardResults.Clear();
            if (value == null) return;
            foreach (var kv in value)
            {
                _boardResults[kv.Key] = kv.Value;
            }
        }
    }

    public Game(GameId id, string? language = null)
    {
        Id = id;
        Language = string.IsNullOrWhiteSpace(language) ? "Français_OFF" : language.Trim();
    }

    // Backward/compat helper used by use cases: check if a given player is admin based on player flag.
    // This is resilient even if AdminPlayerId wasn't present in older JSON snapshots.
    public bool IsAdmin(PlayerId playerId)
        => _players.TryGetValue(playerId, out var p) && p.IsAdmin;

    public void AddPlayer(Player player)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Cannot join after game start.");
        _players[player.Id] = player;

        // Set admin if this player is marked as admin
        if (player.IsAdmin)
        {
            AdminPlayerId = player.Id;
        }
    }

    public async Task InitializeWordsPoolAsync(IWordDictionary wordDictionary, CancellationToken ct = default)
    {
        if (_wordsPool != null)
            throw new InvalidOperationException("WordsPool already initialized.");

        _wordsPool = await WordsPool.CreateAsync(Id, Language, wordDictionary, ct);
    }

    // In persistence via EF, the words pool is not serialized. After reloading a Game from the database
    // the field will be null. Any operation that needs to create cards must ensure the pool is available.
    public bool IsWordsPoolInitialized => _wordsPool != null;

    public Task EnsureWordsPoolInitializedAsync(IWordDictionary wordDictionary, CancellationToken ct = default)
    {
        if (_wordsPool != null)
            return Task.CompletedTask;
        return InitializeWordsPoolAsync(wordDictionary, ct);
    }

    public async Task UpdateLanguageAsync(string newLanguage, IWordDictionary wordDictionary, CancellationToken ct = default)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Language can only be changed in the Lobby phase.");

        if (string.IsNullOrWhiteSpace(newLanguage))
            throw new ArgumentException("Language cannot be empty.", nameof(newLanguage));

        var trimmed = newLanguage.Trim();
        if (!string.Equals(Language, trimmed, StringComparison.Ordinal))
        {
            Language = trimmed;
            // Reinitialize WordsPool with new language
            _wordsPool = null;
            await InitializeWordsPoolAsync(wordDictionary, ct);
        }
    }

    public void UpdateDurationOverrides(int? cluesDurationSeconds, int? guessDurationSeconds)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Durations can only be changed in the Lobby phase.");

        // Validate and clamp within 1..1800 if provided
        if (cluesDurationSeconds.HasValue)
        {
            var v = Math.Clamp(cluesDurationSeconds.Value, 1, 1800);
            CluesDurationSecondsOverride = v;
        }
        if (guessDurationSeconds.HasValue)
        {
            var v = Math.Clamp(guessDurationSeconds.Value, 1, 1800);
            GuessDurationSecondsOverride = v;
        }
    }

    public void SetLobbyDeadline(DateTime nowUtc, TimeSpan duration)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Lobby deadline can only be set during Lobby phase.");
        PhaseEndsAtUtc = nowUtc + duration;
    }

    public void SetScoringDeadline(DateTime nowUtc, TimeSpan duration)
    {
        if (Phase != GamePhase.Scoring)
            throw new InvalidOperationInPhaseException("Scoring deadline can only be set during Scoring phase.");
        PhaseEndsAtUtc = nowUtc + duration;
    }

    public void StartWritingPhase(DateTime nowUtc, TimeSpan duration)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Writing phase can only start from Lobby.");
        if (_players.Count == 0)
            throw new NotEnoughPlayersException(1, _players.Count);
        if (_wordsPool == null)
            throw new InvalidOperationException("WordsPool must be initialized before starting writing phase.");
        Phase = GamePhase.WritingClues;
        PhaseEndsAtUtc = nowUtc + duration;
    }

    public Card CreateRandomCard()
    {
        if (_wordsPool == null)
            throw new InvalidOperationException("WordsPool not initialized.");

        var cardFactory = new CardFactory(_wordsPool);
        return cardFactory.CreateRandomCard(CardId.New());
    }

    public void SetClue(PlayerId playerId, Direction direction, string clueText)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Cannot set clues outside WritingClues phase.");
        var player = RequirePlayer(playerId);
        player.Board.SetClue(direction, ClueText.Create(clueText));
    }

    public void StartGuessingPhase(PlayerId firstBoardOwner, Card fifthCard, Rotation[] cardRotations, DateTime nowUtc, TimeSpan perBoardDuration)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");

        var owner = RequirePlayer(firstBoardOwner);
        CurrentGuessingBoardOwner = firstBoardOwner;

        // Récupérer les 4 cartes originales du board avec rotations randomisées
        OutsideCards = new List<OrientedCard>
        {
            new OrientedCard(owner.Board.TopLeft!.Card, cardRotations[0]),
            new OrientedCard(owner.Board.TopRight!.Card, cardRotations[1]),
            new OrientedCard(owner.Board.BottomRight!.Card, cardRotations[2]),
            new OrientedCard(owner.Board.BottomLeft!.Card, cardRotations[3]),
            new OrientedCard(fifthCard, cardRotations[4]) // 5ème carte avec rotation aléatoire
        };

        // Réinitialiser les positions devinées
        GuessedCardPositions = new Dictionary<BoardPosition, OrientedCard?>
        {
            { BoardPosition.TopLeft, null },
            { BoardPosition.TopRight, null },
            { BoardPosition.BottomRight, null },
            { BoardPosition.BottomLeft, null }
        };

        RemainingAttempts = 3;
        CorrectlyPlacedPositions = new HashSet<BoardPosition>();
        CompletedBoardsCount = 0;

        // Initialize scoring tracking
        _currentBoardStartTime = nowUtc;
        _currentBoardAttempts = 0;

        Phase = GamePhase.Guessing;
        PhaseEndsAtUtc = nowUtc + perBoardDuration;
    }

    public void PlaceCardOnGuessingBoard(int outsideCardIndex, BoardPosition position)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only place cards during Guessing phase.");

        if (outsideCardIndex < 0 || outsideCardIndex >= OutsideCards.Count)
            throw new ArgumentOutOfRangeException(nameof(outsideCardIndex));

        // Si la position est verrouillée (correctement devinée), ne pas permettre le placement
        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot place card on a locked position.");

        // Si la position est déjà occupée, remettre la carte dans OutsideCards
        if (GuessedCardPositions[position] != null)
        {
            OutsideCards.Add(GuessedCardPositions[position]!);
        }

        // Placer la nouvelle carte
        var card = OutsideCards[outsideCardIndex];
        GuessedCardPositions[position] = card;
        OutsideCards.RemoveAt(outsideCardIndex);
    }

    public void SwapGuessingCards(BoardPosition position1, BoardPosition position2)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only swap cards during Guessing phase.");

        // Si l'une des positions est verrouillée, ne pas permettre l'échange
        if (CorrectlyPlacedPositions.Contains(position1) || CorrectlyPlacedPositions.Contains(position2))
            throw new InvalidOperationException("Cannot swap locked positions.");

        // Échanger les cartes
        (GuessedCardPositions[position1], GuessedCardPositions[position2]) =
            (GuessedCardPositions[position2], GuessedCardPositions[position1]);
    }

    // Unified card rotation method - handles both board and outside cards
    public void RotateCard(BoardPosition position, bool rotateRight = true)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");

        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot rotate a locked card.");

        var card = GuessedCardPositions[position];
        if (card == null)
            throw new InvalidOperationException("No card at this position to rotate.");

        GuessedCardPositions[position] = rotateRight ? card.RotateRight() : card.RotateLeft();
    }

    public void RotateCard(int outsideCardIndex, bool rotateRight = true)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");

        if (outsideCardIndex < 0 || outsideCardIndex >= OutsideCards.Count)
            throw new ArgumentOutOfRangeException(nameof(outsideCardIndex));

        OutsideCards[outsideCardIndex] = rotateRight
            ? OutsideCards[outsideCardIndex].RotateRight()
            : OutsideCards[outsideCardIndex].RotateLeft();
    }

    public GuessValidationResult ValidateGuessingBoard()
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only validate during Guessing phase.");

        if (CurrentGuessingBoardOwner == null)
            throw new InvalidOperationException("No current guessing board owner.");

        // Vérifier que toutes les positions sont remplies
        if (GuessedCardPositions.Values.Any(c => c == null))
            throw new InvalidOperationException("All positions must be filled before validation.");

        var owner = RequirePlayer(CurrentGuessingBoardOwner.Value);
        var originalBoard = owner.Board;

        var correctPositions = new List<BoardPosition>();
        var incorrectPositions = new List<BoardPosition>();

        // Vérifier chaque position
        foreach (var (position, guessedCard) in GuessedCardPositions)
        {
            if (guessedCard == null) continue;
            if (CorrectlyPlacedPositions.Contains(position)) continue; // Déjà validée

            var originalCard = position switch
            {
                BoardPosition.TopLeft => originalBoard.TopLeft,
                BoardPosition.TopRight => originalBoard.TopRight,
                BoardPosition.BottomRight => originalBoard.BottomRight,
                BoardPosition.BottomLeft => originalBoard.BottomLeft,
                _ => null
            };

            // Vérifier si c'est la bonne carte ET la bonne rotation
            if (originalCard != null &&
                guessedCard.Card.Id == originalCard.Card.Id &&
                guessedCard.Rotation == originalCard.Rotation)
            {
                correctPositions.Add(position);
                CorrectlyPlacedPositions.Add(position);
            }
            else
            {
                incorrectPositions.Add(position);
            }
        }

        // Remettre les cartes incorrectes dans OutsideCards
        foreach (var pos in incorrectPositions)
        {
            var card = GuessedCardPositions[pos];
            if (card != null)
            {
                OutsideCards.Add(card);
                GuessedCardPositions[pos] = null;
            }
        }

        RemainingAttempts--;
        _currentBoardAttempts++;

        var isComplete = CorrectlyPlacedPositions.Count == 4;
        var shouldMoveToNext = RemainingAttempts == 0 || isComplete;

        // Si le board est complété avec succès, enregistrer le résultat avec le timestamp actuel
        if (isComplete && CurrentGuessingBoardOwner != null)
        {
            var endTime = DateTime.UtcNow;
            var duration = endTime - _currentBoardStartTime;

            _boardResults[CurrentGuessingBoardOwner.Value] = new BoardResult(
                CurrentGuessingBoardOwner.Value,
                _currentBoardAttempts,
                _currentBoardStartTime,
                endTime,
                duration,
                true // wasGuessed = true
            );
        }
        // Si c'est la dernière tentative et le board n'est pas complété, enregistrer l'échec
        else if (RemainingAttempts == 0 && !isComplete && CurrentGuessingBoardOwner != null)
        {
            var endTime = DateTime.UtcNow;
            var duration = endTime - _currentBoardStartTime;

            _boardResults[CurrentGuessingBoardOwner.Value] = new BoardResult(
                CurrentGuessingBoardOwner.Value,
                _currentBoardAttempts,
                _currentBoardStartTime,
                endTime,
                duration,
                false // wasGuessed = false
            );
        }

        return new GuessValidationResult(
            correctPositions,
            incorrectPositions,
            RemainingAttempts,
            isComplete,
            shouldMoveToNext
        );
    }

    public void MoveToNextGuessingBoard(Card? fifthCard, Rotation[]? cardRotations, DateTime nowUtc, TimeSpan perBoardDuration)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only move to next board during Guessing phase.");

        // Le résultat du board a déjà été enregistré dans ValidateGuessingBoard
        // On ne fait que passer au board suivant

        // Incrémenter le compteur de boards complétés
        CompletedBoardsCount++;

        var playersList = Players.ToList();
        
        Console.WriteLine($"[DEBUG_LOG] Game.MoveToNextGuessingBoard: CompletedBoardsCount={CompletedBoardsCount}, TotalPlayers={playersList.Count}");

        // Vérifier si tous les boards ont été devinés
        if (CompletedBoardsCount >= playersList.Count)
        {
            Console.WriteLine($"[DEBUG_LOG] Game.MoveToNextGuessingBoard: Transitioning to Scoring phase.");
            // Tous les boards ont été complétés, fin de la phase de guessing
            Phase = GamePhase.Scoring;
            CurrentGuessingBoardOwner = null;
            PhaseEndsAtUtc = null;
        }
        else
        {
            if (fifthCard == null || cardRotations == null || cardRotations.Length < 5)
                throw new InvalidOperationException("Fifth card and 5 rotations are required for next board.");

            // Trouver le prochain joueur qui n'a pas encore été deviné
            var currentIndex = playersList.FindIndex(p => p.Id == CurrentGuessingBoardOwner);
            var nextIndex = (currentIndex + 1) % playersList.Count;

            // Passer au board suivant
            var nextOwner = playersList[nextIndex];
            CurrentGuessingBoardOwner = nextOwner.Id;

            // Réinitialiser l'état de guessing pour le nouveau board
            OutsideCards = new List<OrientedCard>
            {
                new OrientedCard(nextOwner.Board.TopLeft!.Card, cardRotations[0]),
                new OrientedCard(nextOwner.Board.TopRight!.Card, cardRotations[1]),
                new OrientedCard(nextOwner.Board.BottomRight!.Card, cardRotations[2]),
                new OrientedCard(nextOwner.Board.BottomLeft!.Card, cardRotations[3]),
                new OrientedCard(fifthCard, cardRotations[4])
            };

            GuessedCardPositions = new Dictionary<BoardPosition, OrientedCard?>
            {
                { BoardPosition.TopLeft, null },
                { BoardPosition.TopRight, null },
                { BoardPosition.BottomRight, null },
                { BoardPosition.BottomLeft, null }
            };

            RemainingAttempts = 3;
            CorrectlyPlacedPositions = new HashSet<BoardPosition>();

            // Réinitialiser le tracking pour le nouveau board
            _currentBoardStartTime = nowUtc;
            _currentBoardAttempts = 0;
            PhaseEndsAtUtc = nowUtc + perBoardDuration;
        }
    }

    // Record a timeout loss for the current board if it hasn't been fully guessed.
    // Safe to call multiple times; it will overwrite with the same values for the same board.
    public void RecordTimeoutLoss(DateTime nowUtc)
    {
        if (Phase != GamePhase.Guessing) return;
        if (CurrentGuessingBoardOwner is null) return;

        var isComplete = CorrectlyPlacedPositions.Count == 4;
        if (isComplete) return; // nothing to record

        var endTime = nowUtc;
        var duration = endTime - _currentBoardStartTime;
        _boardResults[CurrentGuessingBoardOwner.Value] = new BoardResult(
            CurrentGuessingBoardOwner.Value,
            _currentBoardAttempts,
            _currentBoardStartTime,
            endTime,
            duration,
            false // wasGuessed = false
        );
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

    public void CompleteGame(PlayerId playerId)
    {
        if (Phase != GamePhase.Scoring)
            throw new InvalidOperationInPhaseException("Can only complete game from Scoring phase.");
        if (playerId != AdminPlayerId)
            throw new UnauthorizedAccessException("Only the admin can complete the game.");

        Phase = GamePhase.Completed;
    }

    private Player RequirePlayer(PlayerId playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            throw new PlayerNotFoundException(playerId);
        return player;
    }
}

public readonly record struct GuessResult(bool IsCorrect, string ExpectedWord);

public readonly record struct GuessValidationResult(
    IReadOnlyList<BoardPosition> CorrectPositions,
    IReadOnlyList<BoardPosition> IncorrectPositions,
    int RemainingAttempts,
    bool IsComplete,
    bool ShouldMoveToNext
);

public readonly record struct BoardResult(
    PlayerId PlayerId,
    int Attempts,
    DateTime StartTime,
    DateTime EndTime,
    TimeSpan Duration,
    bool WasGuessed
);


