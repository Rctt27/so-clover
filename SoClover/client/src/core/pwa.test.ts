import { describe, it, expect, vi } from 'vitest'
import {
  evaluateStandalone,
  evaluateIsIOS,
  isA2HSDismissed,
  setA2HSDismissed,
  evaluateShouldShowA2HSHint,
  STANDALONE_QUERY,
  A2HS_DISMISSED_KEY,
} from './pwa'

describe('evaluateStandalone', () => {
  it('vrai via navigator.standalone (iOS)', () => {
    expect(evaluateStandalone(() => ({ matches: false }), true)).toBe(true)
  })
  it('vrai via la média-query (display-mode: standalone)', () => {
    const matchMedia = vi.fn((q: string) => ({ matches: q === STANDALONE_QUERY }))
    expect(evaluateStandalone(matchMedia, undefined)).toBe(true)
    expect(matchMedia).toHaveBeenCalledWith(STANDALONE_QUERY)
  })
  it('faux quand ni navigator.standalone ni la média-query', () => {
    expect(evaluateStandalone(() => ({ matches: false }), false)).toBe(false)
  })
  it('faux (sans throw) quand matchMedia est indisponible', () => {
    expect(evaluateStandalone(undefined, undefined)).toBe(false)
  })
})

describe('evaluateIsIOS', () => {
  it('vrai pour un UA iPhone', () => {
    expect(evaluateIsIOS('Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)')).toBe(true)
  })
  it('vrai pour un UA iPad', () => {
    expect(evaluateIsIOS('Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X)')).toBe(true)
  })
  it('faux pour un UA desktop', () => {
    expect(evaluateIsIOS('Mozilla/5.0 (Windows NT 10.0; Win64; x64)')).toBe(false)
  })
  it('faux (sans throw) quand l\'UA est indéfini', () => {
    expect(evaluateIsIOS(undefined)).toBe(false)
  })
})

describe('isA2HSDismissed / setA2HSDismissed', () => {
  it('lit "true" depuis le storage', () => {
    const storage = { getItem: vi.fn(() => 'true') }
    expect(isA2HSDismissed(storage)).toBe(true)
    expect(storage.getItem).toHaveBeenCalledWith(A2HS_DISMISSED_KEY)
  })
  it('faux quand la clé est absente', () => {
    expect(isA2HSDismissed({ getItem: () => null })).toBe(false)
  })
  it('faux (sans throw) quand le storage est indisponible', () => {
    expect(isA2HSDismissed(undefined)).toBe(false)
  })
  it('persiste le rejet via setItem', () => {
    const storage = { setItem: vi.fn() }
    setA2HSDismissed(storage)
    expect(storage.setItem).toHaveBeenCalledWith(A2HS_DISMISSED_KEY, 'true')
  })
  it('no-op (sans throw) quand le storage est indisponible', () => {
    expect(() => setA2HSDismissed(undefined)).not.toThrow()
  })
})

describe('evaluateShouldShowA2HSHint', () => {
  it('vrai : iOS, pas standalone, pas rejeté', () => {
    expect(evaluateShouldShowA2HSHint({ isIOS: true, isStandalone: false, isDismissed: false })).toBe(true)
  })
  it('faux : pas iOS', () => {
    expect(evaluateShouldShowA2HSHint({ isIOS: false, isStandalone: false, isDismissed: false })).toBe(false)
  })
  it('faux : déjà en standalone', () => {
    expect(evaluateShouldShowA2HSHint({ isIOS: true, isStandalone: true, isDismissed: false })).toBe(false)
  })
  it('faux : déjà rejeté', () => {
    expect(evaluateShouldShowA2HSHint({ isIOS: true, isStandalone: false, isDismissed: true })).toBe(false)
  })
})
