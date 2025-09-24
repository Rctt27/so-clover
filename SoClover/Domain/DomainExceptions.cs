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

public class InvalidGuessException : DomainException
{
    public InvalidGuessException(string message) : base(message) { }
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


