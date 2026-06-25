import { describe, it, expect } from 'vitest'
import { getClueErrorMessage } from './clueValidationMessages'
import type { ClueValidationError } from './clueValidation'

// Fake i18next `t` : renvoie "<clé>|<param interpolé>" pour vérifier clé + interpolation.
const fakeT = ((key: string, opts?: Record<string, unknown>) => {
  const param = opts ? Object.values(opts)[0] : undefined
  return param === undefined ? key : `${key}|${param}`
}) as never

describe('getClueErrorMessage', () => {
  it('maps TooLong to clueError.tooLong with max', () => {
    const error: ClueValidationError = { rule: 'TooLong', cardWord: '', maxLength: 14 }
    expect(getClueErrorMessage(error, fakeT)).toBe('clueError.tooLong|14')
  })

  it('maps ExactMatch to clueError.exactMatch with word', () => {
    const error: ClueValidationError = { rule: 'ExactMatch', cardWord: 'soleil' }
    expect(getClueErrorMessage(error, fakeT)).toBe('clueError.exactMatch|soleil')
  })
})
