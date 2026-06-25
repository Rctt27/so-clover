import { describe, it, expect, vi } from 'vitest'
import { create } from 'zustand'
import { createAppConfigSlice, AppConfigSlice } from './appConfigSlice'

vi.mock('../../api/game-api', () => ({
  gameApi: {
    getPublicConfig: vi.fn().mockResolvedValue({ aiPlayersEnabled: true, clueMaxLength: 14 }),
  },
}))

describe('appConfigSlice', () => {
  it('loads aiPlayersEnabled and clueMaxLength from config', async () => {
    const store = create<AppConfigSlice>()(createAppConfigSlice as any)

    expect(store.getState().clueMaxLength).toBeNull()
    await store.getState().loadConfig()

    expect(store.getState().aiPlayersEnabled).toBe(true)
    expect(store.getState().clueMaxLength).toBe(14)
  })
})
