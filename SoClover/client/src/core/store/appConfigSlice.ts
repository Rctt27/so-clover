import { StateCreator } from 'zustand'
import { gameApi } from '../../api/game-api'

export interface AppConfigSlice {
  aiPlayersEnabled: boolean | null
  loadConfig: () => Promise<void>
}

export const createAppConfigSlice: StateCreator<AppConfigSlice, [["zustand/devtools", never]]> = (set) => ({
  aiPlayersEnabled: null,

  loadConfig: async () => {
    const config = await gameApi.getPublicConfig()
    set({ aiPlayersEnabled: config.aiPlayersEnabled }, false, 'AppConfigStore/loadConfig')
  },
})
