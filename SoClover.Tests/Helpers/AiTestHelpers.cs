using SoClover.Domain;
using SoClover.Domain.Validation;

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

    /// <summary>
    /// Génère <paramref name="count"/> indices distincts garantis valides contre
    /// le board donné (aucun conflit R1/R2). Évite la flakiness : des mots de carte
    /// tirés aléatoirement pourraient entrer en collision avec des indices codés en dur.
    /// </summary>
    public static string[] PickSafeClues(CloverBoard board, int count)
    {
        var validator = new FrenchOffClueValidator();
        var results = new List<string>();
        for (var i = 0; results.Count < count && i < 5000; i++)
        {
            var candidate = $"zzqxkj{i:D4}";
            var r = validator.Validate(candidate, Direction.Top, board);
            if (r.IsValid) results.Add(candidate);
        }
        if (results.Count < count)
            throw new InvalidOperationException($"Could not generate {count} safe clues for this board.");
        return results.ToArray();
    }
}
