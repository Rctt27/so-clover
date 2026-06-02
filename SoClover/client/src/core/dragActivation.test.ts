import { describe, it, expect } from 'vitest'
import {
  DRAG_ACTIVATION_DISTANCE,
  activationDistanceForPointer,
  isDragActivated,
} from './dragActivation'

describe('activationDistanceForPointer', () => {
  it('uses the larger touch threshold for touch pointers', () => {
    expect(activationDistanceForPointer('touch')).toBe(DRAG_ACTIVATION_DISTANCE.touch)
  })
  it('uses the mouse threshold for mouse pointers', () => {
    expect(activationDistanceForPointer('mouse')).toBe(DRAG_ACTIVATION_DISTANCE.mouse)
  })
  it('treats pen like a mouse (precise pointer)', () => {
    expect(activationDistanceForPointer('pen')).toBe(DRAG_ACTIVATION_DISTANCE.mouse)
  })
  it('falls back to the mouse threshold for unknown/empty pointer types', () => {
    expect(activationDistanceForPointer('')).toBe(DRAG_ACTIVATION_DISTANCE.mouse)
  })
  it('touch threshold is strictly larger than mouse threshold', () => {
    expect(DRAG_ACTIVATION_DISTANCE.touch).toBeGreaterThan(DRAG_ACTIVATION_DISTANCE.mouse)
  })
})

describe('isDragActivated', () => {
  // Cœur du bug : un micro-jitter tactile sous le seuil ne doit PAS activer de drag
  // (sinon un clic sur le coin pour tourner se transforme en swap avec une carte voisine).
  it('does NOT activate on a small touch jitter below the touch threshold', () => {
    expect(isDragActivated(10, 0, 'touch')).toBe(false) // 10px < 16px
  })
  it('does NOT activate on a diagonal touch jitter below the touch threshold', () => {
    // hypot(8, 8) ≈ 11.3px < 16px
    expect(isDragActivated(8, 8, 'touch')).toBe(false)
  })
  it('activates a touch drag once the touch threshold is reached', () => {
    expect(isDragActivated(16, 0, 'touch')).toBe(true)
  })
  it('activates a mouse drag at the mouse threshold', () => {
    expect(isDragActivated(DRAG_ACTIVATION_DISTANCE.mouse, 0, 'mouse')).toBe(true)
  })
  it('does NOT activate a mouse drag below the mouse threshold', () => {
    expect(isDragActivated(DRAG_ACTIVATION_DISTANCE.mouse - 1, 0, 'mouse')).toBe(false)
  })
  it('a 10px move that activates a mouse drag stays a click on touch', () => {
    // Même geste physique, verdict opposé selon le type de pointeur.
    expect(isDragActivated(10, 0, 'mouse')).toBe(true)
    expect(isDragActivated(10, 0, 'touch')).toBe(false)
  })
})
