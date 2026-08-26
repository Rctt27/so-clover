import { describe, it, expect, vi, beforeEach } from 'vitest'
import { create } from 'zustand'
import { createAppConfigSlice, AppConfigSlice } from './appConfigSlice'
import { gameApi } from '../../api/game-api'

vi.mock('../../api/game-api', () => ({
  gameApi: {
    getPublicConfig: vi.fn(),
  },
}))

const mockConfig = (config: Record<string, unknown>) =>
  vi.mocked(gameApi.getPublicConfig).mockResolvedValue(config as never)

const newStore = () => create<AppConfigSlice>()(createAppConfigSlice as any)

describe('appConfigSlice', () => {
  beforeEach(() => vi.clearAllMocks())

  it('loads aiPlayersEnabled and clueMaxLength from config', async () => {
    mockConfig({ aiPlayersEnabled: true, aiPlayersUnavailableReason: null, clueMaxLength: 14 })
    const store = newStore()

    expect(store.getState().clueMaxLength).toBeNull()
    await store.getState().loadConfig()

    expect(store.getState().aiPlayersEnabled).toBe(true)
    expect(store.getState().clueMaxLength).toBe(14)
    expect(store.getState().aiPlayersUnavailableReason).toBeNull()
  })

  it('keeps the reason returned when the LLM API key is unavailable', async () => {
    mockConfig({
      aiPlayersEnabled: false,
      aiPlayersUnavailableReason: 'apiKeyUnavailable',
      clueMaxLength: 14,
    })
    const store = newStore()

    await store.getState().loadConfig()

    expect(store.getState().aiPlayersEnabled).toBe(false)
    expect(store.getState().aiPlayersUnavailableReason).toBe('apiKeyUnavailable')
  })

  it('keeps the reason returned when the feature flag is off', async () => {
    mockConfig({ aiPlayersEnabled: false, aiPlayersUnavailableReason: 'disabled', clueMaxLength: 14 })
    const store = newStore()

    await store.getState().loadConfig()

    expect(store.getState().aiPlayersUnavailableReason).toBe('disabled')
  })

  it('falls back to null when the server omits the reason field', async () => {
    // Serveur antérieur à la sonde de clé API : le champ n'existe pas encore.
    mockConfig({ aiPlayersEnabled: true, clueMaxLength: 14 })
    const store = newStore()

    await store.getState().loadConfig()

    expect(store.getState().aiPlayersUnavailableReason).toBeNull()
  })
})
