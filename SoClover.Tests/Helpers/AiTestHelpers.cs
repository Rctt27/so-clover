using SoClover.Domain;

namespace SoClover.Tests.Helpers;

/// <summary>
/// Helpers pour les tests AI Epic 03+. Simule l'auto-submit qui sera implémenté
/// par Epic 06 — ici on marque simplement le board comme submitted via l'API publique
/// du domaine, en posant aussi 4 cards si elles ne sont pas déjà placées (sinon
/// StartGuessingPhase rejettera le board comme incomplet).
/// </summary>
public static class AiTestHelpers
{
    /// <summary>
    /// Place 4 cards et marque le board d'un AI comme submitted.
    /// Les cards utilisent le WordsPool de la game (game.CreateRandomCard()) — la game
    /// doit donc avoir initialisé son pool (StartWritingPhase déjà appelé) avant.
    /// </summary>
    public static void SimulateAiBoardSubmit(Game game, PlayerId aiPlayerId, DateTime nowUtc)
    {
        var ai = game.Players.FirstOrDefault(p => p.Id == aiPlayerId)
            ?? throw new InvalidOperationException($"Player {aiPlayerId} not found in game.");
        if (!ai.IsAI)
            throw new InvalidOperationException($"Player {aiPlayerId} is not flagged IsAI=true.");

        if (ai.Board.TopLeft == null)
        {
            ai.Board.Place(BoardPosition.TopLeft,     new OrientedCard(game.CreateRandomCard()));
            ai.Board.Place(BoardPosition.TopRight,    new OrientedCard(game.CreateRandomCard()));
            ai.Board.Place(BoardPosition.BottomRight, new OrientedCard(game.CreateRandomCard()));
            ai.Board.Place(BoardPosition.BottomLeft,  new OrientedCard(game.CreateRandomCard()));
        }

        ai.Board.SetClue(Direction.Top,    ClueText.Create("ai-top"));
        ai.Board.SetClue(Direction.Right,  ClueText.Create("ai-right"));
        ai.Board.SetClue(Direction.Bottom, ClueText.Create("ai-bottom"));
        ai.Board.SetClue(Direction.Left,   ClueText.Create("ai-left"));

        ai.Board.MarkSubmitted(nowUtc);
    }
}
