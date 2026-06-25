import { StateCreator } from 'zustand'
import { gameApi } from '../../api/game-api'

export interface AppConfigSlice {
  aiPlayersEnabled: boolean | null
  clueMaxLength: number | null
  loadConfig: () => Promise<void>
}

export const createAppConfigSlice: StateCreator<AppConfigSlice, [["zustand/devtools", never]]> = (set) => ({
  aiPlayersEnabled: null,
  clueMaxLength: null,

  loadConfig: async () => {
    const config = await gameApi.getPublicConfig()
    set(
      { aiPlayersEnabled: config.aiPlayersEnabled, clueMaxLength: config.clueMaxLength },
      false,
      'AppConfigStore/loadConfig',
    )
  },
})
