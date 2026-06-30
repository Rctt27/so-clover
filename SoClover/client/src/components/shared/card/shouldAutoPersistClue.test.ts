import { describe, it, expect } from 'vitest'
import { shouldAutoPersistClue } from './shouldAutoPersistClue'

describe('shouldAutoPersistClue', () => {
  const base = {
    localValue: 'voiture',
    savedValue: '',
    isValid: true,
    isChecking: false,
    disabled: false,
  }

  it('persiste un indice valide, non vide, encore non enregistré et stabilisé', () => {
    expect(shouldAutoPersistClue(base)).toBe(true)
  })

  it("ne persiste pas tant que la validation serveur est en cours", () => {
    expect(shouldAutoPersistClue({ ...base, isChecking: true })).toBe(false)
  })

  it('ne persiste pas un indice invalide', () => {
    expect(shouldAutoPersistClue({ ...base, isValid: false })).toBe(false)
  })

  it('ne persiste pas un indice vide (ou uniquement des espaces)', () => {
    expect(shouldAutoPersistClue({ ...base, localValue: '   ' })).toBe(false)
  })

  it("ne persiste pas si l'indice est déjà enregistré côté serveur (pas de re-POST)", () => {
    expect(shouldAutoPersistClue({ ...base, localValue: 'voiture', savedValue: 'voiture' })).toBe(false)
  })

  it("ignore les espaces de bordure pour comparer à la valeur enregistrée", () => {
    expect(shouldAutoPersistClue({ ...base, localValue: '  voiture  ', savedValue: 'voiture' })).toBe(false)
  })

  it("ne persiste pas en lecture seule (input disabled — phases Guessing/Scoring)", () => {
    expect(shouldAutoPersistClue({ ...base, disabled: true })).toBe(false)
  })
})
