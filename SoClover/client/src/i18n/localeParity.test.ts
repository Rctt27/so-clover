import { describe, it, expect } from 'vitest'
import { resources } from './index'

type AnyObj = Record<string, unknown>

function flattenKeys(obj: AnyObj, prefix = ''): string[] {
  return Object.entries(obj).flatMap(([k, v]) => {
    const key = prefix ? `${prefix}.${k}` : k
    return v && typeof v === 'object' && !Array.isArray(v)
      ? flattenKeys(v as AnyObj, key)
      : [key]
  })
}

const NAMESPACES = ['common', 'home', 'lobby', 'writing', 'guessing', 'scoring'] as const

describe('locale key parity', () => {
  for (const ns of NAMESPACES) {
    it(`fr and pt match en for namespace "${ns}"`, () => {
      const en = flattenKeys(resources.en[ns] as AnyObj).sort()
      const fr = flattenKeys(resources.fr[ns] as AnyObj).sort()
      const pt = flattenKeys(resources.pt[ns] as AnyObj).sort()
      expect(fr).toEqual(en)
      expect(pt).toEqual(en)
    })
  }
})
