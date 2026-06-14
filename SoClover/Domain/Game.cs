using System.Text.Json.Serialization;
using SoClover.Domain.Validation;

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
    
    [JsonInclude]
    [JsonPropertyName("semanticClueCheckEnabled")]
    public bool SemanticClueCheckEnabled { get; private set; }

    [JsonInclude]
    [JsonPropertyName("guessAiBoardOnly")]
    public bool GuessAiBoardOnly { get; private set; }

    private const int CURSOR_COLORS_COUNT = 10;
    private readonly Dictionary<PlayerId, Player> _players = new();
    private WordsPool? _wordsPool;

    // Guessing phase state
    [JsonInclude]
    [JsonPropertyName("currentGuessingBoardOwner")]
    public PlayerId? CurrentGuessingBoardOwner { get; private set; }

    [JsonInclude]
    [JsonPropertyName("outsideCards")]
    public List<OrientedCard?> OutsideCards { get; private set; } = new();

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
    [JsonPropertyName("failedPlacements")]
    public List<FailedPlacement> FailedPlacements { get; private set; } = new();

    [JsonInclude]
    [JsonPropertyName("completedBoardsCount")]
    public int CompletedBoardsCount { get; private set; } = 0;

    [JsonInclude]
    [JsonPropertyName("cumulativeBoardRotation")]
    public int CumulativeBoardRotation { get; private set; } = 0;

    [JsonInclude]
    [JsonPropertyName("guessingBoardRevealed")]
    public bool GuessingBoardRevealed { get; private set; }

    [JsonInclude]
    [JsonPropertyName("revision")]
    public int Revision { get; private set; } = 0;

    private void BumpRevision() => Revision++;

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

    [JsonIgnore]
    public IReadOnlyCollection<Player> ActivePlayers =>
        _players.Values.Where(p => !p.IsDisconnected).ToList().AsReadOnly();

    [JsonIgnore]
    public IReadOnlyCollection<Player> GuessingParticipants =>
        _players.Values.Where(p => !p.IsDisconnected && !p.IsAI).ToList().AsReadOnly();

    [JsonIgnore]
    public IReadOnlyCollection<Player> WritingParticipants =>
        GuessAiBoardOnly
            ? _players.Values.Where(p => !p.IsDisconnected && p.IsAI).ToList().AsReadOnly()
            : ActivePlayers;

    [JsonIgnore]
    public IReadOnlyCollection<Player> BoardsToGuess =>
        _players.Values.Where(p => !p.IsDisconnected && p.Board.IsSubmitted).ToList().AsReadOnly();

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
        SemanticClueCheckEnabled = SemanticValidationSupport.IsSupported(Language);
    }

    // Backward/compat helper used by use cases: check if a given player is admin based on player flag.
    // This is resilient even if AdminPlayerId wasn't present in older JSON snapshots.
    public bool IsAdmin(PlayerId playerId)
        => _players.TryGetValue(playerId, out var p) && p.IsAdmin;

    public void AddPlayer(Player player)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Impossible de rejoindre : la partie a déjà commencé.");
        BumpRevision();

        // Attribuer une couleur de curseur au joueur
        player.SetCursorColorIndex(GetNextAvailableColorIndex());

        _players[player.Id] = player;

        // Set admin if this player is marked as admin
        if (player.IsAdmin)
        {
            AdminPlayerId = player.Id;
        }
    }

    public void AddAIPlayer(Player player, int max)
    {
        if (!player.IsAI)
            throw new ArgumentException("Player must be flagged as AI.", nameof(player));

        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Cannot add AI player after game start.");

        var currentAiCount = _players.Values.Count(p => p.IsAI);
        if (currentAiCount >= max)
            throw new MaxAIPlayersReachedException(currentAiCount, max);

        AddPlayer(player);
    }

    public void RemovePlayer(PlayerId playerId)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Cannot leave after game start.");
        BumpRevision();

        _players.Remove(playerId);

        if (AdminPlayerId == playerId)
        {
            AdminPlayerId = _players.Count > 0 ? _players.Keys.First() : null;
            if (AdminPlayerId is PlayerId newAdminId)
            {
                _players[newAdminId].IsAdmin = true;
            }
        }

        if (GuessAiBoardOnly && !_players.Values.Any(p => p.IsAI && !p.IsDisconnected))
        {
            GuessAiBoardOnly = false;
        }
    }

    public Player? FindPlayerByName(string name)
    {
        var trimmed = name.Trim();
        return _players.Values.FirstOrDefault(p =>
            string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public PlayerId ReplacePlayer(PlayerId existingId, Player newPlayer)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Cannot replace player after game start.");
        BumpRevision();
        if (!_players.ContainsKey(existingId))
            throw new PlayerNotFoundException(existingId);

        var existing = _players[existingId];
        var replacement = new Player(existingId, newPlayer.Name, existing.IsAdmin);
        replacement.SetCursorColorIndex(existing.CursorColorIndex);
        _players[existingId] = replacement;
        return existingId;
    }

    public void DisconnectPlayerDuringWriting(PlayerId playerId)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Can only disconnect during WritingClues phase.");
        BumpRevision();

        var player = RequirePlayer(playerId);
        if (player.IsDisconnected)
            return; // already disconnected, idempotent

        player.MarkDisconnected();

        _boardResults[playerId] = new BoardResult(
            playerId,
            Attempts: 0,
            StartTime: DateTime.UtcNow,
            EndTime: DateTime.UtcNow,
            Duration: TimeSpan.Zero,
            WasGuessed: false,
            IsDisconnected: true
        );
    }

    /// <summary>
    /// Réactive un joueur marqué déconnecté quand il rejoint. Idempotent.
    /// Limité à WritingClues : en aval (Guessing/Scoring) le board est déjà scoré,
    /// on ne touche pas à l'état pour préserver l'intégrité du résultat.
    /// Retourne true seulement si une réactivation a effectivement eu lieu.
    /// </summary>
    public bool ReconnectPlayer(PlayerId playerId)
    {
        var player = RequirePlayer(playerId);
        if (!player.IsDisconnected)
            return false;
        if (Phase != GamePhase.WritingClues)
            return false;

        BumpRevision();
        player.MarkReconnected();

        if (_boardResults.TryGetValue(playerId, out var br) && br.IsDisconnected)
            _boardResults.Remove(playerId);

        return true;
    }

    public async Task<WordsPool> InitializeWordsPoolAsync(IWordDictionary wordDictionary, CancellationToken ct = default)
    {
        if (_wordsPool != null)
            throw new InvalidOperationException("WordsPool already initialized.");

        _wordsPool = await WordsPool.CreateAsync(Id, Language, wordDictionary, ct);
        return _wordsPool;
    }

    public bool IsWordsPoolInitialized => _wordsPool != null;

    public void AttachWordsPool(WordsPool pool)
    {
        _wordsPool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    public void UpdateLanguage(string newLanguage)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Language can only be changed in the Lobby phase.");
        BumpRevision();

        if (string.IsNullOrWhiteSpace(newLanguage))
            throw new ArgumentException("Language cannot be empty.", nameof(newLanguage));

        var trimmed = newLanguage.Trim();
        if (!string.Equals(Language, trimmed, StringComparison.Ordinal))
        {
            Language = trimmed;
            _wordsPool = null;
            if (!SemanticValidationSupport.IsSupported(Language))
                SemanticClueCheckEnabled = false;
        }
    }
    
    public void SetSemanticClueCheckEnabled(bool enabled)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Semantic clue check can only be toggled in the Lobby phase.");
        BumpRevision();

        if (enabled && !SemanticValidationSupport.IsSupported(Language))
            throw new InvalidOperationException("Semantic clue check is only available for dictionaries with semantic validation support.");

        SemanticClueCheckEnabled = enabled;
    }

    public void SetGuessAiBoardOnly(bool enabled)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("GuessAiBoardOnly can only be toggled in the Lobby phase.");

        if (enabled && !_players.Values.Any(p => p.IsAI && !p.IsDisconnected))
            throw new NoAiPlayerForGuessAiBoardOnlyException();

        GuessAiBoardOnly = enabled;
    }

    public void UpdateDurationOverrides(int? cluesDurationSeconds, int? guessDurationSeconds)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Durations can only be changed in the Lobby phase.");
        BumpRevision();

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
        BumpRevision();
        PhaseEndsAtUtc = nowUtc + duration;
    }

    public void SetScoringDeadline(DateTime nowUtc, TimeSpan duration)
    {
        if (Phase != GamePhase.Scoring)
            throw new InvalidOperationInPhaseException("Scoring deadline can only be set during Scoring phase.");
        BumpRevision();
        PhaseEndsAtUtc = nowUtc + duration;
    }

    public void StartWritingPhase(DateTime nowUtc, TimeSpan duration)
    {
        if (Phase != GamePhase.Lobby)
            throw new InvalidOperationInPhaseException("Writing phase can only start from Lobby.");
        BumpRevision();
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

    public ClueValidationResult SetClue(
        PlayerId playerId,
        Direction direction,
        string clueText,
        IClueValidator validator)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Cannot set clues outside WritingClues phase.");
        BumpRevision();

        var player = RequirePlayer(playerId);
        var parsed = ClueText.Create(clueText);
        var result = validator.Validate(parsed.Value, direction, player.Board);

        if (!result.IsValid)
        {
            player.Board.ClearClue(direction);
            return result;
        }

        player.Board.SetClue(direction, parsed);
        return result;
    }

    public void StartGuessingPhase(PlayerId firstBoardOwner, Card fifthCard, Rotation[] cardRotations, DateTime nowUtc, TimeSpan perBoardDuration)
    {
        if (Phase != GamePhase.WritingClues)
            throw new InvalidOperationInPhaseException("Guessing phase can only start after WritingClues.");
        BumpRevision();

        var owner = RequirePlayer(firstBoardOwner);
        if (owner.Board.TopLeft == null || owner.Board.TopRight == null || owner.Board.BottomRight == null || owner.Board.BottomLeft == null)
            throw new InvalidOperationException("Cannot start guessing phase with an incomplete board.");

        CurrentGuessingBoardOwner = firstBoardOwner;

        // Récupérer les 4 cartes originales du board avec rotations randomisées
        OutsideCards = new List<OrientedCard?>
        {
            new OrientedCard(owner.Board.TopLeft!.Card, cardRotations[0]),
            new OrientedCard(owner.Board.TopRight!.Card, cardRotations[1]),
            new OrientedCard(owner.Board.BottomRight!.Card, cardRotations[2]),
            new OrientedCard(owner.Board.BottomLeft!.Card, cardRotations[3]),
            new OrientedCard(fifthCard, cardRotations[4]), // 5ème carte avec rotation aléatoire
            null // 6ème slot vide
        };

        // Randomiser l'ordre des cartes dans le pool pour éviter toute déduction basée sur la position
        ShuffleOutsideCards();

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
        FailedPlacements = new List<FailedPlacement>();
        CompletedBoardsCount = 0;
        GuessingBoardRevealed = false;

        // Initialize scoring tracking
        _currentBoardStartTime = nowUtc;
        _currentBoardAttempts = 0;

        Phase = GamePhase.Guessing;
        PhaseEndsAtUtc = nowUtc + perBoardDuration;
        CumulativeBoardRotation = 0;
    }

    public void PlaceCardOnGuessingBoard(int outsideCardIndex, BoardPosition position)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only place cards during Guessing phase.");
        BumpRevision();

        if (outsideCardIndex < 0 || outsideCardIndex >= OutsideCards.Count)
            throw new ArgumentOutOfRangeException(nameof(outsideCardIndex));

        // Si la position est verrouillée (correctement devinée), ne pas permettre le placement
        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot place card on a locked position.");

        var newCard = OutsideCards[outsideCardIndex];
        if (newCard == null)
            throw new InvalidOperationException("No card at the specified pool index.");

        OrientedCard? displacedCard = null;
        // Si la position est déjà occupée, on va remettre cette carte dans le pool à l'index utilisé
        if (GuessedCardPositions[position] != null)
        {
            displacedCard = GuessedCardPositions[position];
        }

        // Placer la nouvelle carte avec compensation de la rotation du plateau
        // pour conserver son orientation absolue (visuelle par rapport au joueur).
        int stepsToCompensate = CumulativeBoardRotation / 90;
        GuessedCardPositions[position] = newCard.Rotate(-stepsToCompensate);
        
        // Mettre la carte déplacée dans le pool avec la rotation inverse (si nécessaire)
        if (displacedCard != null)
        {
            OutsideCards[outsideCardIndex] = displacedCard.Rotate(stepsToCompensate);
        }
        else
        {
            OutsideCards[outsideCardIndex] = null;
        }
    }

    public void SwapGuessingCards(BoardPosition position1, BoardPosition position2)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only swap cards during Guessing phase.");
        BumpRevision();

        // Si l'une des positions est verrouillée, ne pas permettre l'échange
        if (CorrectlyPlacedPositions.Contains(position1) || CorrectlyPlacedPositions.Contains(position2))
            throw new InvalidOperationException("Cannot swap locked positions.");

        // Échanger les cartes
        (GuessedCardPositions[position1], GuessedCardPositions[position2]) =
            (GuessedCardPositions[position2], GuessedCardPositions[position1]);
    }

    public void SwapOutsidePoolCards(int index1, int index2)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only swap pool cards during Guessing phase.");
        BumpRevision();

        if (index1 < 0 || index1 >= OutsideCards.Count || index2 < 0 || index2 >= OutsideCards.Count)
            throw new ArgumentOutOfRangeException("Indices must be within the range of OutsideCards.");

        (OutsideCards[index1], OutsideCards[index2]) = (OutsideCards[index2], OutsideCards[index1]);
    }

    public void ReturnGuessingCard(BoardPosition position)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only return cards during Guessing phase.");
        BumpRevision();

        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot return a locked card.");

        var card = GuessedCardPositions[position];
        if (card != null)
        {
            // Inverser la compensation lors du retour dans le pool
            int stepsToInvert = CumulativeBoardRotation / 90;
            var orientedCard = card.Rotate(stepsToInvert);
            
            // Trouver le premier slot vide dans le pool
            int emptySlotIndex = OutsideCards.FindIndex(c => c == null);
            if (emptySlotIndex != -1)
            {
                OutsideCards[emptySlotIndex] = orientedCard;
            }
            else
            {
                OutsideCards.Add(orientedCard);
            }
            
            GuessedCardPositions[position] = null;
        }
    }

    // Unified card rotation method - handles both board and outside cards
    public void RotateCard(BoardPosition position, int steps)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");
        BumpRevision();

        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot rotate a locked card.");

        var card = GuessedCardPositions[position];
        if (card == null)
        {
            Console.WriteLine($"[DEBUG_LOG] Domain Game.RotateCard Error: No card found at position {position}");
            throw new InvalidOperationException("No card at this position to rotate.");
        }

        GuessedCardPositions[position] = card.Rotate(steps);
        Console.WriteLine($"[DEBUG_LOG] Domain Game.RotateCard Success: Position={position}, NewSteps={steps}, NewRotation={GuessedCardPositions[position]?.Rotation}");
    }

    public void RotateCard(int outsideCardIndex, int steps)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");
        BumpRevision();

        if (outsideCardIndex < 0 || outsideCardIndex >= OutsideCards.Count)
        {
            Console.WriteLine($"[DEBUG_LOG] Domain Game.RotateCard Error: Invalid OutsideCardIndex {outsideCardIndex}. Pool size={OutsideCards.Count}");
            throw new ArgumentOutOfRangeException(nameof(outsideCardIndex));
        }

        var card = OutsideCards[outsideCardIndex];
        if (card == null)
        {
            throw new InvalidOperationException("No card at the specified pool index to rotate.");
        }

        OutsideCards[outsideCardIndex] = card.Rotate(steps);
        
        Console.WriteLine($"[DEBUG_LOG] Domain Game.RotateCard Success: OutsideIndex={outsideCardIndex}, NewSteps={steps}, NewRotation={OutsideCards[outsideCardIndex]!.Rotation}");
    }

    public GuessValidationResult ValidateGuessingBoard()
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only validate during Guessing phase.");
        BumpRevision();

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
                var failed = new FailedPlacement(position, guessedCard.Card.Id.Value, guessedCard.Rotation);
                if (!FailedPlacements.Contains(failed))
                    FailedPlacements.Add(failed);
            }
        }

        // Remettre les cartes incorrectes dans OutsideCards.
        // Inverser la compensation de rotation du plateau (symétrie avec ReturnGuessingCard) :
        // les cartes du board sont stockées en repère board-relatif, le pool est en repère absolu.
        // Sans cette inversion, une carte revenue au pool serait mal orientée et, une fois re-posée,
        // subirait de nouveau la compensation → double compensation → orientation incohérente.
        int stepsToInvert = CumulativeBoardRotation / 90;
        foreach (var pos in incorrectPositions)
        {
            var card = GuessedCardPositions[pos];
            if (card != null)
            {
                var orientedCard = card.Rotate(stepsToInvert);
                // Trouver le premier slot vide dans le pool
                int emptySlotIndex = OutsideCards.FindIndex(c => c == null);
                if (emptySlotIndex != -1)
                {
                    OutsideCards[emptySlotIndex] = orientedCard;
                }
                else
                {
                    OutsideCards.Add(orientedCard);
                }
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

            Console.WriteLine($"[DEBUG_LOG] Game.ValidateGuessingBoard: SUCCESS for {CurrentGuessingBoardOwner.Value}. Attempts: {_currentBoardAttempts}");

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

            Console.WriteLine($"[DEBUG_LOG] Game.ValidateGuessingBoard: FAILURE for {CurrentGuessingBoardOwner.Value}. Attempts: {_currentBoardAttempts}");

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

    /// <summary>
    /// True si, après le passage au board suivant, on doit transiter en Scoring
    /// (i.e. on est en train de quitter le dernier board à deviner).
    /// Centralise la sémantique pour que les UseCases ne recalculent pas localement.
    /// </summary>
    public bool IsLastGuessingBoard()
    {
        var boardsCount = BoardsToGuess.Count;
        if (boardsCount == 0) return false;
        return CompletedBoardsCount >= boardsCount - 1;
    }

    public void MoveToNextGuessingBoard(Card? fifthCard, Rotation[]? cardRotations, DateTime nowUtc, TimeSpan perBoardDuration)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only move to next board during Guessing phase.");
        BumpRevision();

        // Le résultat du board a déjà été enregistré dans ValidateGuessingBoard
        // On ne fait que passer au board suivant

        // Incrémenter le compteur de boards complétés
        CompletedBoardsCount++;

        var boardsList = BoardsToGuess.ToList();

        Console.WriteLine($"[DEBUG_LOG] Game.MoveToNextGuessingBoard: CompletedBoardsCount={CompletedBoardsCount}, BoardsToGuess={boardsList.Count}");

        // Vérifier si tous les boards ont été devinés (boards submitted, AI inclus)
        if (CompletedBoardsCount >= boardsList.Count)
        {
            Console.WriteLine($"[DEBUG_LOG] Game.MoveToNextGuessingBoard: Transitioning to Scoring phase.");
            // Tous les boards ont été complétés, fin de la phase de guessing
            Phase = GamePhase.Scoring;
            CurrentGuessingBoardOwner = null;
            PhaseEndsAtUtc = null;
            GuessingBoardRevealed = false;
        }
        else
        {
            if (fifthCard == null || cardRotations == null || cardRotations.Length < 5)
                throw new InvalidOperationException("Fifth card and 5 rotations are required for next board.");

            // Trouver le prochain board qui n'a pas encore été deviné
            var currentIndex = boardsList.FindIndex(p => p.Id == CurrentGuessingBoardOwner);
            // Si l'owner courant n'est plus dans BoardsToGuess (cas: déconnecté en cours de Guessing),
            // on repart du premier board disponible.
            var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % boardsList.Count;

            // Passer au board suivant
            var nextOwner = boardsList[nextIndex];
            CurrentGuessingBoardOwner = nextOwner.Id;

            // Réinitialiser l'état de guessing pour le nouveau board
            OutsideCards = new List<OrientedCard?>
            {
                new OrientedCard(nextOwner.Board.TopLeft!.Card, cardRotations[0]),
                new OrientedCard(nextOwner.Board.TopRight!.Card, cardRotations[1]),
                new OrientedCard(nextOwner.Board.BottomRight!.Card, cardRotations[2]),
                new OrientedCard(nextOwner.Board.BottomLeft!.Card, cardRotations[3]),
                new OrientedCard(fifthCard, cardRotations[4]),
                null
            };

            // Randomiser l'ordre des cartes dans le pool pour éviter toute déduction basée sur la position
            ShuffleOutsideCards();

            GuessedCardPositions = new Dictionary<BoardPosition, OrientedCard?>
            {
                { BoardPosition.TopLeft, null },
                { BoardPosition.TopRight, null },
                { BoardPosition.BottomRight, null },
                { BoardPosition.BottomLeft, null }
            };

            RemainingAttempts = 3;
            CorrectlyPlacedPositions = new HashSet<BoardPosition>();
            FailedPlacements = new List<FailedPlacement>();

            // Réinitialiser le tracking pour le nouveau board
            _currentBoardStartTime = nowUtc;
            _currentBoardAttempts = 0;
            PhaseEndsAtUtc = nowUtc + perBoardDuration;
            CumulativeBoardRotation = 0; // On réinitialise la rotation pour le nouveau plateau
            GuessingBoardRevealed = false;
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

        Console.WriteLine($"[DEBUG_LOG] Game.RecordTimeoutLoss: FAILURE for {CurrentGuessingBoardOwner.Value}. Attempts: {_currentBoardAttempts}");

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
        BumpRevision();
        var player = RequirePlayer(ownerId);
        var expected = player.Board.GetClueText(direction);
        var correct = string.Equals(expected, guessedWord?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (correct)
        {
            player.Board.MarkGuessed(direction);
        }
        return new GuessResult(correct, expected);
    }

    public void CompleteGame(PlayerId playerId)
    {
        if (Phase != GamePhase.Scoring)
            throw new InvalidOperationInPhaseException("Can only complete game from Scoring phase.");
        if (playerId != AdminPlayerId)
            throw new UnauthorizedAccessException("Only the admin can complete the game.");
    }

    public void RotateBoard(int rotation)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate board during Guessing phase.");

        BumpRevision();
        CumulativeBoardRotation = rotation;
    }

    /// <summary>
    /// Démarre le cooldown de débrief après l'échec d'un board (timeout ou 3 tentatives).
    /// Révèle la solution (via le gate de GetGameState) et fixe la deadline de cooldown.
    /// Idempotent : sans effet si le cooldown est déjà actif.
    /// </summary>
    public void StartGuessingCooldown(DateTime nowUtc, TimeSpan cooldown)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only start cooldown during Guessing phase.");
        if (GuessingBoardRevealed) return;

        GuessingBoardRevealed = true;
        PhaseEndsAtUtc = nowUtc + cooldown;
        BumpRevision();
    }

    private int GetNextAvailableColorIndex()
    {
        var usedColors = _players.Values.Select(p => p.CursorColorIndex).ToHashSet();

        // Chercher une couleur non utilisée (1-10)
        for (int i = 1; i <= CURSOR_COLORS_COUNT; i++)
        {
            if (!usedColors.Contains(i))
                return i;
        }

        // Toutes les couleurs sont utilisées : attribution aléatoire
        return Random.Shared.Next(1, CURSOR_COLORS_COUNT + 1);
    }

    private Player RequirePlayer(PlayerId playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            throw new PlayerNotFoundException(playerId);
        return player;
    }

    /// <summary>
    /// Shuffles the OutsideCards list (excluding the last null slot) using Fisher-Yates algorithm.
    /// This ensures cards are randomly positioned in the pool, making deduction harder.
    /// </summary>
    private void ShuffleOutsideCards()
    {
        // We shuffle only the first 5 elements (indices 0-4), keeping the 6th slot (index 5) as null
        var cardsToShuffle = OutsideCards.Take(5).ToList();

        // Fisher-Yates shuffle
        for (int i = cardsToShuffle.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (cardsToShuffle[i], cardsToShuffle[j]) = (cardsToShuffle[j], cardsToShuffle[i]);
        }

        // Rebuild the OutsideCards list with shuffled cards + null slot
        OutsideCards = new List<OrientedCard?>(cardsToShuffle) { null };
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
    bool WasGuessed,
    bool IsDisconnected = false
);


