using SoClover.Domain;

namespace SoClover.UseCases.GameLogics;

// Simple event to notify clients that the writing clues phase will end soon
public readonly record struct WritingCountdownWarning(GameId GameId, int SecondsRemaining);
