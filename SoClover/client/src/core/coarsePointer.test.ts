import { describe, it, expect, vi } from 'vitest'
import { evaluateCoarsePointer, subscribeCoarsePointer, COARSE_POINTER_QUERY } from './coarsePointer'

describe('evaluateCoarsePointer', () => {
  it('vrai quand la média-query (pointer: coarse) matche', () => {
    const matchMedia = vi.fn((query: string) => ({ matches: query === COARSE_POINTER_QUERY }))

    expect(evaluateCoarsePointer(matchMedia)).toBe(true)
    expect(matchMedia).toHaveBeenCalledWith(COARSE_POINTER_QUERY)
  })

  it('faux quand la média-query ne matche pas (pointeur précis)', () => {
    expect(evaluateCoarsePointer(() => ({ matches: false }))).toBe(false)
  })

  it('faux (sans throw) quand matchMedia est indisponible', () => {
    expect(evaluateCoarsePointer(undefined)).toBe(false)
  })
})

describe('subscribeCoarsePointer', () => {
  it('enregistre un listener change et le retire au cleanup', () => {
    const add = vi.fn()
    const remove = vi.fn()
    const matchMedia = vi.fn(() => ({ matches: false, addEventListener: add, removeEventListener: remove }))
    const onChange = vi.fn()

    const cleanup = subscribeCoarsePointer(matchMedia, onChange)

    expect(matchMedia).toHaveBeenCalledWith(COARSE_POINTER_QUERY)
    expect(add).toHaveBeenCalledWith('change', onChange)

    cleanup()
    expect(remove).toHaveBeenCalledWith('change', onChange)
  })

  it('renvoie un cleanup no-op (sans throw) quand matchMedia est indisponible', () => {
    const cleanup = subscribeCoarsePointer(undefined, () => {})
    expect(() => cleanup()).not.toThrow()
  })

  it('tolère un MediaQueryList sans addEventListener (vieux Safari)', () => {
    const matchMedia = vi.fn(() => ({ matches: true }))
    const cleanup = subscribeCoarsePointer(matchMedia, () => {})
    expect(() => cleanup()).not.toThrow()
  })
})
