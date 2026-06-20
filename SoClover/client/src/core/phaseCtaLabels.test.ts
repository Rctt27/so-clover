import { describe, it, expect } from 'vitest'
import { getWritingSubmitLabel, getGuessingValidateLabel } from './phaseCtaLabels'

const base = { isSubmitting: false, isSubmitted: false, canSubmit: false, anyChecking: false, allCluesFilled: false }

describe('getWritingSubmitLabel', () => {
  it('priorise « soumission en cours »', () => {
    expect(getWritingSubmitLabel({ ...base, isSubmitting: true }, false)).toBe('Soumission...')
    expect(getWritingSubmitLabel({ ...base, isSubmitting: true }, true)).toBe('Soumission…')
  })

  it('état soumis', () => {
    expect(getWritingSubmitLabel({ ...base, isSubmitted: true }, false)).toBe('Plateau Soumis ✓')
    expect(getWritingSubmitLabel({ ...base, isSubmitted: true }, true)).toBe('Soumis ✓')
  })

  it('prêt à soumettre', () => {
    expect(getWritingSubmitLabel({ ...base, canSubmit: true }, false)).toBe('Soumettre le plateau')
    expect(getWritingSubmitLabel({ ...base, canSubmit: true }, true)).toBe('Soumettre')
  })

  it('vérification sémantique en cours', () => {
    expect(getWritingSubmitLabel({ ...base, anyChecking: true }, false)).toBe('Vérification…')
    expect(getWritingSubmitLabel({ ...base, anyChecking: true }, true)).toBe('Vérification…')
  })

  it('indices remplis mais invalides → corrigez', () => {
    expect(getWritingSubmitLabel({ ...base, allCluesFilled: true }, false)).toBe('Corrigez les indices')
    expect(getWritingSubmitLabel({ ...base, allCluesFilled: true }, true)).toBe('Corrigez')
  })

  it('indices manquants (défaut)', () => {
    expect(getWritingSubmitLabel(base, false)).toBe('Saisissez les 4 indices')
    expect(getWritingSubmitLabel(base, true)).toBe('Indices manquants')
  })
})

describe('getGuessingValidateLabel', () => {
  it('plateau résolu → suivant', () => {
    expect(getGuessingValidateLabel(true, false)).toBe('Plateau suivant')
    expect(getGuessingValidateLabel(true, true)).toBe('Suivant')
  })

  it('plateau non résolu → valider', () => {
    expect(getGuessingValidateLabel(false, false)).toBe('Valider le plateau')
    expect(getGuessingValidateLabel(false, true)).toBe('Valider')
  })
})
