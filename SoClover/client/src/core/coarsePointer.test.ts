import { describe, it, expect, vi } from 'vitest'
import { evaluateCoarsePointer, COARSE_POINTER_QUERY } from './coarsePointer'

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
