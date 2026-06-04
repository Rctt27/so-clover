import { describe, it, expect } from 'vitest'
import { nextReconnectDelay } from './reconnectionPolicy'
import { CONSTANTS } from './constants'

describe('nextReconnectDelay', () => {
  it('renvoie le premier délai au premier essai', () => {
    expect(nextReconnectDelay({ previousRetryCount: 0, elapsedMilliseconds: 0 }))
      .toBe(CONSTANTS.RECONNECT.delaysMs[0])
  })

  it('progresse dans le tableau de délais', () => {
    expect(nextReconnectDelay({ previousRetryCount: 2, elapsedMilliseconds: 7000 }))
      .toBe(CONSTANTS.RECONNECT.delaysMs[2])
  })

  it('répète le dernier délai au-delà du tableau', () => {
    const last = CONSTANTS.RECONNECT.delaysMs[CONSTANTS.RECONNECT.delaysMs.length - 1]
    expect(nextReconnectDelay({ previousRetryCount: 99, elapsedMilliseconds: 120000 }))
      .toBe(last)
  })

  it('abandonne (null) une fois maxElapsedMs dépassé', () => {
    expect(nextReconnectDelay({ previousRetryCount: 99, elapsedMilliseconds: CONSTANTS.RECONNECT.maxElapsedMs }))
      .toBeNull()
  })
})
