import { describe, it, expect, beforeEach } from 'vitest'
import { create, type UseBoundStore, type StoreApi } from 'zustand'
import { createGuessingSlice, GuessingSlice } from './guessingSlice'

describe('applyServerRotation', () => {
  let store: UseBoundStore<StoreApi<GuessingSlice>>

  beforeEach(() => {
    store = create<GuessingSlice>()(createGuessingSlice as any)
  })

  it('applies the first rotation', () => {
    const applied = store.getState().applyServerRotation(90, 1)
    expect(applied).toBe(true)
    expect(store.getState().cumulativeBoardRotation).toBe(90)
    expect(store.getState().lastAppliedRotationRevision).toBe(1)
  })

  it('rejects a rotation with stale revision', () => {
    store.getState().applyServerRotation(90, 5)
    const applied = store.getState().applyServerRotation(180, 3)
    expect(applied).toBe(false)
    expect(store.getState().cumulativeBoardRotation).toBe(90)
    expect(store.getState().lastAppliedRotationRevision).toBe(5)
  })

  it('rejects a rotation with equal revision', () => {
    store.getState().applyServerRotation(90, 5)
    const applied = store.getState().applyServerRotation(180, 5)
    expect(applied).toBe(false)
    expect(store.getState().cumulativeBoardRotation).toBe(90)
  })

  it('applies a rotation with newer revision regardless of value direction', () => {
    store.getState().applyServerRotation(180, 3)
    const applied = store.getState().applyServerRotation(0, 4)
    expect(applied).toBe(true)
    expect(store.getState().cumulativeBoardRotation).toBe(0)
    expect(store.getState().lastAppliedRotationRevision).toBe(4)
  })
})

describe('failedPlacements', () => {
  it('defaults to an empty array', () => {
    const store = create<GuessingSlice>()(createGuessingSlice as any)
    expect(store.getState().failedPlacements).toEqual([])
  })

  it('is cleared by resetGuessingState', () => {
    const store = create<GuessingSlice>()(createGuessingSlice as any)
    store.getState().setGuessingState({
      failedPlacements: [{ position: 'TopLeft', cardId: 'c1', rotation: 'None' }],
    })
    expect(store.getState().failedPlacements).toHaveLength(1)
    store.getState().resetGuessingState()
    expect(store.getState().failedPlacements).toEqual([])
  })
})
