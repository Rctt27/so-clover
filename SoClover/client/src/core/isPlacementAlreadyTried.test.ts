import { describe, it, expect } from 'vitest'
import { isPlacementAlreadyTried } from './isPlacementAlreadyTried'
import type { FailedPlacementInfo } from '../types/game'

const hist: FailedPlacementInfo[] = [
  { position: 'TopLeft', cardId: 'c1', rotation: 'None' },
  { position: 'TopRight', cardId: 'c2', rotation: 'Clockwise90' },
]

describe('isPlacementAlreadyTried', () => {
  it('matches an exact tried placement', () => {
    expect(isPlacementAlreadyTried(hist, 'c1', 'TopLeft', 'None')).toBe(true)
  })
  it('normalizes rotation string variants (Right90 ≡ Clockwise90)', () => {
    expect(isPlacementAlreadyTried(hist, 'c2', 'TopRight', 'Right90')).toBe(true)
  })
  it('does not match a different rotation', () => {
    expect(isPlacementAlreadyTried(hist, 'c1', 'TopLeft', 'Clockwise90')).toBe(false)
  })
  it('does not match a different position', () => {
    expect(isPlacementAlreadyTried(hist, 'c1', 'BottomLeft', 'None')).toBe(false)
  })
  it('does not match a different card', () => {
    expect(isPlacementAlreadyTried(hist, 'cX', 'TopLeft', 'None')).toBe(false)
  })
  it('returns false for empty history', () => {
    expect(isPlacementAlreadyTried([], 'c1', 'TopLeft', 'None')).toBe(false)
  })
})
