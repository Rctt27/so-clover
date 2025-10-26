namespace SoClover.Domain;

public sealed class Game
{
    public GameId Id { get; }
    public string Language { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    private readonly Dictionary<PlayerId, Player> _players = new();

    // Guessing phase state
    public PlayerId? CurrentGuessingBoardOwner { get; private set; }
    public List<OrientedCard> OutsideCards { get; private set; } = new();
    public Dictionary<BoardPosition, OrientedCard?> GuessedCardPositions { get; private set; } = new()
    {
        { BoardPosition.TopLeft, null },
        { BoardPosition.TopRight, null },
        { BoardPosition.BottomRight, null },
        { BoardPosition.BottomLeft, null }
    };
    public int RemainingAttempts { get; private set; } = 3;
    public HashSet<BoardPosition> CorrectlyPlacedPositions { get; private set; } = new();

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

    public void StartGuessingPhase(PlayerId firstBoardOwner, Card fifthCard, Rotation[] cardRotations)
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

        Phase = GamePhase.Guessing;
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

    public void RotateGuessingCard(BoardPosition position)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");

        if (CorrectlyPlacedPositions.Contains(position))
            throw new InvalidOperationException("Cannot rotate a locked card.");

        var card = GuessedCardPositions[position];
        if (card == null)
            throw new InvalidOperationException("No card at this position to rotate.");

        GuessedCardPositions[position] = card.RotateRight();
    }

    public void RotateOutsideCard(int outsideCardIndex)
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only rotate cards during Guessing phase.");

        if (outsideCardIndex < 0 || outsideCardIndex >= OutsideCards.Count)
            throw new ArgumentOutOfRangeException(nameof(outsideCardIndex));

        OutsideCards[outsideCardIndex] = OutsideCards[outsideCardIndex].RotateRight();
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

        var isComplete = correctPositions.Count == 4;
        var shouldMoveToNext = RemainingAttempts == 0 || isComplete;

        return new GuessValidationResult(
            correctPositions,
            incorrectPositions,
            RemainingAttempts,
            isComplete,
            shouldMoveToNext
        );
    }

    public void MoveToNextGuessingBoard()
    {
        if (Phase != GamePhase.Guessing)
            throw new InvalidOperationInPhaseException("Can only move to next board during Guessing phase.");

        // Trouver le prochain joueur
        var playersList = Players.ToList();
        var currentIndex = playersList.FindIndex(p => p.Id == CurrentGuessingBoardOwner);
        var nextIndex = (currentIndex + 1) % playersList.Count;

        if (nextIndex == 0)
        {
            // On a fait le tour de tous les boards, fin de la phase de guessing
            Phase = GamePhase.Scoring;
            CurrentGuessingBoardOwner = null;
        }
        else
        {
            // Passer au board suivant - nécessite de regénérer les cartes
            // Cette méthode sera appelée après la génération de la 5ème carte
            CurrentGuessingBoardOwner = playersList[nextIndex].Id;
        }
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

public readonly record struct GuessValidationResult(
    IReadOnlyList<BoardPosition> CorrectPositions,
    IReadOnlyList<BoardPosition> IncorrectPositions,
    int RemainingAttempts,
    bool IsComplete,
    bool ShouldMoveToNext
);


