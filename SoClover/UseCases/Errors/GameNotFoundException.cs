using SoClover.Domain;

namespace SoClover.UseCases.Errors;

public class GameNotFoundException : Exception
{
    public GameId GameId { get; }

    public GameNotFoundException(GameId gameId)
        : base($"Game not found: {gameId}")
    {
        GameId = gameId;
    }
}
