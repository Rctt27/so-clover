using SoClover.Domain;

namespace SoClover.UseCases.Games;

// Simple event to notify clients that the current guessing board will end soon
public readonly record struct GuessingCountdownWarning(GameId GameId, int SecondsRemaining);
