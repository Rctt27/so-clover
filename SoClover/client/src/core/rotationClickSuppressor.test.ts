import { describe, it, expect } from 'vitest'
import { createRotationClickSuppressor } from './rotationClickSuppressor'

describe('createRotationClickSuppressor', () => {
  it('ne supprime rien tant qu\'aucune rotation n\'a été armée', () => {
    const s = createRotationClickSuppressor()
    expect(s.consume(0)).toBe(false)
  })

  it('supprime le click synthétique qui suit immédiatement une rotation', () => {
    const s = createRotationClickSuppressor()
    s.arm(0)
    // Le click natif arrive quelques ms après le pointerup de rotation.
    expect(s.consume(5)).toBe(true)
  })

  it('ne supprime qu\'UN seul click (single-shot) — un second click passe', () => {
    const s = createRotationClickSuppressor()
    s.arm(0)
    expect(s.consume(5)).toBe(true)
    expect(s.consume(6)).toBe(false)
  })

  it('n\'affecte pas un click légitime arrivant bien après (hors fenêtre)', () => {
    const s = createRotationClickSuppressor(300)
    s.arm(0)
    // Un vrai clic-clic sur le corps, 400ms plus tard, ne doit pas être avalé.
    expect(s.consume(400)).toBe(false)
  })

  it('un nouvel armement réinitialise la fenêtre', () => {
    const s = createRotationClickSuppressor(300)
    s.arm(0)
    expect(s.consume(400)).toBe(false) // expiré
    s.arm(1000)
    expect(s.consume(1005)).toBe(true) // ré-armé
  })
})
