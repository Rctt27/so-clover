namespace SoClover.Domain;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class InvalidClueException : DomainException
{
    public InvalidClueException(string message) : base(message) { }
}

public class InvalidOperationInPhaseException : DomainException
{
    public InvalidOperationInPhaseException(string message) : base(message) { }
}

public class NoClueForDirectionException : DomainException
{
    public Direction Direction { get; }

    public NoClueForDirectionException(Direction direction)
        : base($"No card placed for direction {direction}.")
    {
        Direction = direction;
    }
}

public class CardWordEmptyException : DomainException
{
    public CardWordEmptyException() : base("Card word cannot be empty.") { }
}

public class CardWordTooLongException : DomainException
{
    public int MaxLength { get; }

    public CardWordTooLongException(int maxLength)
        : base($"Card word cannot exceed {maxLength} characters.")
    {
        MaxLength = maxLength;
    }
}

public class PlayerNameEmptyException : DomainException
{
    public PlayerNameEmptyException() : base("Player name cannot be empty.") { }
}

public class PlayerNameTooLongException : DomainException
{
    public int MaxLength { get; }

    public PlayerNameTooLongException(int maxLength)
        : base($"Player name cannot exceed {maxLength} characters.")
    {
        MaxLength = maxLength;
    }
}

public class NotEnoughPlayersException : DomainException
{
    public int RequiredMinimum { get; }
    public int ActualCount { get; }

    public NotEnoughPlayersException(int requiredMinimum, int actualCount)
        : base($"At least {requiredMinimum} player(s) are required; got {actualCount}.")
    {
        RequiredMinimum = requiredMinimum;
        ActualCount = actualCount;
    }
}

public class PlayerNotFoundException : DomainException
{
    public PlayerId PlayerId { get; }

    public PlayerNotFoundException(PlayerId playerId)
        : base($"Player not found: {playerId}.")
    {
        PlayerId = playerId;
    }
}

public class DisconnectedPlayersException : DomainException
{
    public IReadOnlyList<string> PlayerNames { get; }

    public DisconnectedPlayersException(IReadOnlyList<string> playerNames)
        : base($"The following players appear disconnected: {string.Join(", ", playerNames)}")
    {
        PlayerNames = playerNames;
    }
}
