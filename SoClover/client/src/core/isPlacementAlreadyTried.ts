import { rotationToDegrees, FailedPlacementInfo } from '../types/game'

/**
 * True si (cardId, position, rotation) figure dans l'historique des placements ratés.
 * Compare la rotation en degrés normalisés pour être robuste aux variantes de chaîne
 * backend ("Right90" / "Clockwise90").
 */
export function isPlacementAlreadyTried(
  history: FailedPlacementInfo[],
  cardId: string,
  position: string,
  rotation: string,
): boolean {
  const deg = rotationToDegrees(rotation)
  return history.some(
    (f) => f.cardId === cardId && f.position === position && rotationToDegrees(f.rotation) === deg,
  )
}
