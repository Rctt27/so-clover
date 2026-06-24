import { describe, it, expect } from 'vitest'
import { normalizeLocale, localeToDictionaryKey } from './dictionaryDefaults'

describe('normalizeLocale', () => {
  it('strips region and accepts supported locales', () => {
    expect(normalizeLocale('fr-FR')).toBe('fr')
    expect(normalizeLocale('en-US')).toBe('en')
    expect(normalizeLocale('pt-BR')).toBe('pt')
  })
  it('falls back to en for unsupported or empty', () => {
    expect(normalizeLocale('de')).toBe('en')
    expect(normalizeLocale('')).toBe('en')
    expect(normalizeLocale(null)).toBe('en')
  })
})

describe('localeToDictionaryKey', () => {
  it('maps each UI locale to its default game dictionary', () => {
    expect(localeToDictionaryKey('fr')).toBe('Français_OFF')
    expect(localeToDictionaryKey('en')).toBe('English_(from_FR_OFF)')
    expect(localeToDictionaryKey('pt')).toBe('Portuguese_(from_FR_OFF)')
  })
  it('defaults to the English dictionary for anything else', () => {
    expect(localeToDictionaryKey('de')).toBe('English_(from_FR_OFF)')
  })
})
