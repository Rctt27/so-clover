import { describe, it, expect, beforeAll } from 'vitest'
import i18n from '../i18n'
import { getWritingSubmitLabel, getGuessingValidateLabel } from './phaseCtaLabels'

const tw = () => i18n.getFixedT('fr', 'writing')
const tg = () => i18n.getFixedT('fr', 'guessing')
const base = { isSubmitting: false, isSubmitted: false, canSubmit: false, anyChecking: false, allCluesFilled: false }

beforeAll(async () => { await i18n.changeLanguage('fr') })

describe('getWritingSubmitLabel', () => {
  it('priorise « soumission en cours »', () => {
    expect(getWritingSubmitLabel({ ...base, isSubmitting: true }, false, tw())).toBe('Soumission...')
    expect(getWritingSubmitLabel({ ...base, isSubmitting: true }, true, tw())).toBe('Soumission…')
  })
  it('état soumis', () => {
    expect(getWritingSubmitLabel({ ...base, isSubmitted: true }, false, tw())).toBe('Plateau Soumis ✓')
    expect(getWritingSubmitLabel({ ...base, isSubmitted: true }, true, tw())).toBe('Soumis ✓')
  })
  it('prêt à soumettre', () => {
    expect(getWritingSubmitLabel({ ...base, canSubmit: true }, false, tw())).toBe('Soumettre le plateau')
    expect(getWritingSubmitLabel({ ...base, canSubmit: true }, true, tw())).toBe('Soumettre')
  })
  it('vérification sémantique en cours', () => {
    expect(getWritingSubmitLabel({ ...base, anyChecking: true }, false, tw())).toBe('Vérification…')
    expect(getWritingSubmitLabel({ ...base, anyChecking: true }, true, tw())).toBe('Vérification…')
  })
  it('indices remplis mais invalides → corrigez', () => {
    expect(getWritingSubmitLabel({ ...base, allCluesFilled: true }, false, tw())).toBe('Corrigez les indices')
    expect(getWritingSubmitLabel({ ...base, allCluesFilled: true }, true, tw())).toBe('Corrigez')
  })
  it('indices manquants (défaut)', () => {
    expect(getWritingSubmitLabel(base, false, tw())).toBe('Saisissez les 4 indices')
    expect(getWritingSubmitLabel(base, true, tw())).toBe('Indices manquants')
  })
})

describe('getGuessingValidateLabel', () => {
  it('plateau résolu → suivant', () => {
    expect(getGuessingValidateLabel(true, false, tg())).toBe('Plateau suivant')
    expect(getGuessingValidateLabel(true, true, tg())).toBe('Suivant')
  })
  it('plateau non résolu → valider', () => {
    expect(getGuessingValidateLabel(false, false, tg())).toBe('Valider le plateau')
    expect(getGuessingValidateLabel(false, true, tg())).toBe('Valider')
  })
})
