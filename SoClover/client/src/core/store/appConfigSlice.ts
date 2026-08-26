import { StateCreator } from 'zustand'
import { gameApi, AiPlayersUnavailableReason } from '../../api/game-api'

export interface AppConfigSlice {
  aiPlayersEnabled: boolean | null
  aiPlayersUnavailableReason: AiPlayersUnavailableReason | null
  clueMaxLength: number | null
  loadConfig: () => Promise<void>
}

export const createAppConfigSlice: StateCreator<AppConfigSlice, [["zustand/devtools", never]]> = (set) => ({
  aiPlayersEnabled: null,
  aiPlayersUnavailableReason: null,
  clueMaxLength: null,

  loadConfig: async () => {
    const config = await gameApi.getPublicConfig()
    set(
      {
        aiPlayersEnabled: config.aiPlayersEnabled,
        // Tolère un serveur antérieur à la sonde de clé API, qui n'émet pas encore ce champ.
        aiPlayersUnavailableReason: config.aiPlayersUnavailableReason ?? null,
        clueMaxLength: config.clueMaxLength,
      },
      false,
      'AppConfigStore/loadConfig',
    )
  },
})
