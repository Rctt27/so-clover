import { CardInfoResponse } from '../types/game'
import { LOGICAL_SLOTS } from './utils'

/**
 * Cartes-solution à afficher en opacité réduite pendant le cooldown de débrief.
 * Pour chaque position logique (ordre LOGICAL_SLOTS), renvoie la carte solution
 * SI la solution est révélée ET que la position n'a pas déjà été devinée correctement.
 * Sinon null (la case montre alors sa carte verrouillée existante, ou rien).
 */
export function computeRevealedCards(
  solution: Record<string, CardInfoResponse | null> | null | undefined,
  correctlyPlacedPositions: string[],
): (CardInfoResponse | null)[] {
  return LOGICAL_SLOTS.map((pos) => {
    if (!solution) return null
    if (correctlyPlacedPositions.includes(pos)) return null
    return solution[pos] ?? null
  })
}
