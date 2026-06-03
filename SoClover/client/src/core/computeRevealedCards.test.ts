import { describe, it, expect } from 'vitest'
import { computeRevealedCards } from './computeRevealedCards'
import { CardInfoResponse } from '../types/game'

const card = (id: string): CardInfoResponse => ({
  cardId: id, topWord: 't', rightWord: 'r', bottomWord: 'b', leftWord: 'l', rotation: 'None',
})

describe('computeRevealedCards', () => {
  it('renvoie [null,null,null,null] sans solution', () => {
    expect(computeRevealedCards(null, [])).toEqual([null, null, null, null])
  })

  it('remplit seulement les positions non correctement devinées', () => {
    const solution = {
      TopLeft: card('a'), TopRight: card('b'), BottomRight: card('c'), BottomLeft: card('d'),
    }
    const result = computeRevealedCards(solution, ['TopLeft', 'BottomRight'])
    // Ordre LOGICAL_SLOTS: [TopLeft, TopRight, BottomRight, BottomLeft]
    expect(result[0]).toBeNull()             // TopLeft déjà correct
    expect(result[1]?.cardId).toBe('b')      // TopRight révélé
    expect(result[2]).toBeNull()             // BottomRight déjà correct
    expect(result[3]?.cardId).toBe('d')      // BottomLeft révélé
  })
})
