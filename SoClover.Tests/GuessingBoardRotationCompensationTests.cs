using System.Linq;
using SoClover.Domain;
using SoClover.Tests.Helpers;
using Xunit;

namespace SoClover.Tests;

/// <summary>
/// Bug n°2 : <see cref="Game.ValidateGuessingBoard"/> renvoie les cartes incorrectes dans le pool
/// SANS inverser la compensation de rotation du plateau, contrairement à
/// <see cref="Game.ReturnGuessingCard"/> qui applique bien <c>Rotate(+CumulativeBoardRotation/90)</c>.
///
/// Invariant attendu : poser une carte du pool sur un plateau tourné, puis la récupérer via une
/// validation ratée, doit préserver son orientation absolue (celle qu'elle avait dans le pool).
/// Sinon, la re-poser applique de nouveau la compensation → double compensation → carte mal orientée
/// (le « clipping » observé en jeu).
/// </summary>
public class GuessingBoardRotationCompensationTests
{
    [Fact]
    public void ValidateGuessingBoard_WithRotatedBoard_ReturnsIncorrectCardsToPoolWithOriginalRotation()
    {
        // Arrange — partie en phase Guessing ; toutes les cartes du pool démarrent à Rotation.None.
        var (game, _, _) = GuessingPhaseGameBuilder.CreateGameInGuessingPhase();

        // Le guesser tourne le plateau de 90° (1 quart de tour).
        game.RotateBoard(90);

        // On remplit les 4 positions (la validation l'exige). Sur un plateau tourné de 90°, la
        // compensation au placement stocke chaque carte à Right270 (≠ None) → toutes incorrectes
        // (les cartes d'origine du propriétaire sont à None) → toutes renvoyées au pool.
        var positions = new[]
        {
            BoardPosition.TopLeft,
            BoardPosition.TopRight,
            BoardPosition.BottomRight,
            BoardPosition.BottomLeft,
        };

        var placedCardIds = new System.Collections.Generic.List<CardId>();
        foreach (var pos in positions)
        {
            int idx = game.OutsideCards.FindIndex(c => c != null);
            placedCardIds.Add(game.OutsideCards[idx]!.Card.Id);
            game.PlaceCardOnGuessingBoard(idx, pos);
        }

        // Act
        var result = game.ValidateGuessingBoard();

        // Assert — les 4 cartes sont incorrectes et reviennent au pool dans leur orientation absolue
        // d'origine (Rotation.None). Avec le bug, elles reviennent à Right270 (repère board-relatif).
        Assert.Equal(4, result.IncorrectPositions.Count);
        foreach (var cardId in placedCardIds)
        {
            var returned = game.OutsideCards.FirstOrDefault(c => c != null && c.Card.Id == cardId);
            Assert.NotNull(returned);
            Assert.Equal(Rotation.None, returned!.Rotation);
        }
    }
}
