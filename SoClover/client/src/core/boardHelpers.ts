import {
  BoardData,
  CardData,
  BoardStateResponse,
  rotationToDegrees
} from '../types/game'

/**
 * Convertit les données backend d'un plateau en BoardData utilisable par le client
 * @param boardState - État du plateau depuis le backend
 * @param playerId - ID du joueur propriétaire du plateau
 * @returns BoardData formaté pour le client
 */
export function convertBackendBoardToClientBoard(
  boardState: BoardStateResponse,
  playerId: string
): BoardData {
  // Backend mapping: Direction -> Board Position
  // Direction.Top -> board.TopLeft
  // Direction.Right -> board.TopRight
  // Direction.Bottom -> board.BottomRight
  // Direction.Left -> board.BottomLeft

  // Frontend expects: [TopLeft, TopRight, BottomRight, BottomLeft]
  const topLeftCard = boardState.top.card        // Direction.Top -> TopLeft
  const topRightCard = boardState.right.card     // Direction.Right -> TopRight
  const bottomRightCard = boardState.bottom.card // Direction.Bottom -> BottomRight
  const bottomLeftCard = boardState.left.card    // Direction.Left -> BottomLeft

  /*
  console.log('[boardHelpers] Converting backend board to client board:', {
    topLeftCard,
    topRightCard,
    bottomRightCard,
    bottomLeftCard
  })
  */

  // Convertir chaque carte en CardData
  // Array order: [TopLeft, TopRight, BottomRight, BottomLeft]
  const cards: (CardData | null)[] = [
    topLeftCard ? {
      words: [
        topLeftCard.topWord,
        topLeftCard.rightWord,
        topLeftCard.bottomWord,
        topLeftCard.leftWord
      ] as [string, string, string, string],
      rotation: rotationToDegrees(topLeftCard.rotation)
    } : null,

    topRightCard ? {
      words: [
        topRightCard.topWord,
        topRightCard.rightWord,
        topRightCard.bottomWord,
        topRightCard.leftWord
      ] as [string, string, string, string],
      rotation: rotationToDegrees(topRightCard.rotation)
    } : null,

    bottomRightCard ? {
      words: [
        bottomRightCard.topWord,
        bottomRightCard.rightWord,
        bottomRightCard.bottomWord,
        bottomRightCard.leftWord
      ] as [string, string, string, string],
      rotation: rotationToDegrees(bottomRightCard.rotation)
    } : null,

    bottomLeftCard ? {
      words: [
        bottomLeftCard.topWord,
        bottomLeftCard.rightWord,
        bottomLeftCard.bottomWord,
        bottomLeftCard.leftWord
      ] as [string, string, string, string],
      rotation: rotationToDegrees(bottomLeftCard.rotation)
    } : null
  ]

  // console.log('[boardHelpers] Converted cards:', cards)

  return {
    cards,
    rotation: 0, // Par défaut, pas de rotation du plateau
    isSubmitted: boardState.isSubmitted,
    clues: {
      top: {
        text: boardState.top.clueLabel || '',
        playerId: boardState.top.clueLabel ? playerId : null
      },
      right: {
        text: boardState.right.clueLabel || '',
        playerId: boardState.right.clueLabel ? playerId : null
      },
      bottom: {
        text: boardState.bottom.clueLabel || '',
        playerId: boardState.bottom.clueLabel ? playerId : null
      },
      left: {
        text: boardState.left.clueLabel || '',
        playerId: boardState.left.clueLabel ? playerId : null
      }
    }
  }
}
