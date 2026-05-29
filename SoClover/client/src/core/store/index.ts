import { create, StateCreator } from 'zustand'
import { persist, createJSONStorage, devtools } from 'zustand/middleware'
import { Role, GamePhase, ConnectionStatus } from '../../types/game'
import { createNotificationSlice, NotificationSlice } from './notificationSlice'
import { createBoardSlice, BoardSlice } from './boardSlice'
import { createGuessingSlice, GuessingSlice } from './guessingSlice'
import { createAppConfigSlice, AppConfigSlice } from './appConfigSlice'
import { isDebug } from '../debug'

export interface AiClueRetries {
  top: number
  right: number
  bottom: number
  left: number
}

export interface AiClueProgress {
  submitted: number
  retries: AiClueRetries
}

interface GameState {
  gameId: string | null
  playerId: string | null
  playerName: string | null
  phase: GamePhase
  role: Role
  isGameAdmin: boolean
  adminPlayerId: string | null
  connectionStatus: ConnectionStatus
  players: Array<{ playerId: string, name: string, cursorColorIndex: number, isAI: boolean }>
  isInitializing: boolean
  phaseEndsAtUtc: string | null
  aiGeneratingPlayerIds: string[]
  aiClueProgress: Record<string, AiClueProgress>
  settings: {
    language: string
    cluesDurationSeconds: number
    guessDurationSeconds: number
    semanticClueCheckEnabled: boolean
    guessAiBoardOnly: boolean
  }
  setPhase: (phase: GamePhase) => void
  markAiGenerating: (playerId: string) => void
  clearAiGenerating: (playerId: string) => void
  setAiClueProgress: (playerId: string, submitted: number, retries: AiClueRetries) => void
  clearAiClueProgress: (playerId: string) => void
  setRole: (role: Role) => void
  setIsGameAdmin: (isAdmin: boolean) => void
  setAdminPlayerId: (adminPlayerId: string | null) => void
  setGameId: (gameId: string | null) => void
  setPlayerId: (playerId: string | null) => void
  setPlayerName: (playerName: string | null) => void
  setConnectionStatus: (status: ConnectionStatus) => void
  setPlayers: (players: Array<{ playerId: string, name: string, cursorColorIndex: number, isAI: boolean }>) => void
  setPhaseEndsAtUtc: (deadline: string | null) => void
  setSettings: (settings: { language: string, cluesDurationSeconds: number, guessDurationSeconds: number, semanticClueCheckEnabled: boolean, guessAiBoardOnly: boolean }) => void
  setIsInitializing: (isInitializing: boolean) => void
  resetAuth: () => void
}

const gameStateCreator: StateCreator<GameState, [["zustand/devtools", never]]> = (set) => ({
  gameId: null,
  playerId: null,
  playerName: null,
  phase: 'Initial',
  role: 'PlayerWritingClue',
  isGameAdmin: false,
  adminPlayerId: null,
  isInitializing: false,
  connectionStatus: 'Disconnected',
  players: [],
  phaseEndsAtUtc: null,
  aiGeneratingPlayerIds: [],
  aiClueProgress: {},
  settings: {
    language: 'Français_OFF',
    cluesDurationSeconds: 300,
    guessDurationSeconds: 300,
    semanticClueCheckEnabled: true,
    guessAiBoardOnly: false,
  },
  setPhase: (phase) => set(
    (state) => ({
      phase,
      aiGeneratingPlayerIds: phase === 'WritingClues' ? state.aiGeneratingPlayerIds : [],
      aiClueProgress: phase === 'WritingClues' ? state.aiClueProgress : {},
    }),
    false, 'GameStore/setPhase'
  ),
  markAiGenerating: (playerId) => set(
    (state) => ({
      aiGeneratingPlayerIds: state.aiGeneratingPlayerIds.includes(playerId)
        ? state.aiGeneratingPlayerIds
        : [...state.aiGeneratingPlayerIds, playerId]
    }),
    false, 'GameStore/markAiGenerating'
  ),
  clearAiGenerating: (playerId) => set(
    (state) => ({ aiGeneratingPlayerIds: state.aiGeneratingPlayerIds.filter(id => id !== playerId) }),
    false, 'GameStore/clearAiGenerating'
  ),
  setAiClueProgress: (playerId, submitted, retries) => set(
    (state) => ({
      aiClueProgress: { ...state.aiClueProgress, [playerId]: { submitted, retries } }
    }),
    false, 'GameStore/setAiClueProgress'
  ),
  clearAiClueProgress: (playerId) => set(
    (state) => {
      if (!(playerId in state.aiClueProgress)) return state
      const next = { ...state.aiClueProgress }
      delete next[playerId]
      return { aiClueProgress: next }
    },
    false, 'GameStore/clearAiClueProgress'
  ),
  setRole: (role) => set({ role }, false, 'GameStore/setRole'),
  setIsGameAdmin: (isGameAdmin) => set({ isGameAdmin }, false, 'GameStore/setIsGameAdmin'),
  setAdminPlayerId: (adminPlayerId) => set({ adminPlayerId }, false, 'GameStore/setAdminPlayerId'),
  setGameId: (gameId) => set({ gameId }, false, 'GameStore/setGameId'),
  setPlayerId: (playerId) => set({ playerId }, false, 'GameStore/setPlayerId'),
  setPlayerName: (playerName) => set({ playerName }, false, 'GameStore/setPlayerName'),
  setConnectionStatus: (status) => set({ connectionStatus: status }, false, 'GameStore/setConnectionStatus'),
  setPlayers: (players) => set({ players }, false, 'GameStore/setPlayers'),
  setPhaseEndsAtUtc: (phaseEndsAtUtc) => set({ phaseEndsAtUtc }, false, 'GameStore/setPhaseEndsAtUtc'),
  setSettings: (settings) => set({ settings }, false, 'GameStore/setSettings'),
  setIsInitializing: (isInitializing) => set({ isInitializing }, false, 'GameStore/setIsInitializing'),
  resetAuth: () => set({
    playerId: null,
    playerName: null,
    gameId: null,
    role: 'PlayerWritingClue',
    isGameAdmin: false,
    adminPlayerId: null,
    players: [],
    phase: 'Initial',
    phaseEndsAtUtc: null,
    settings: {
      language: 'Français_OFF',
      cluesDurationSeconds: 300,
      guessDurationSeconds: 300,
      semanticClueCheckEnabled: true,
      guessAiBoardOnly: false,
    }
  }, false, 'GameStore/resetAuth'),
})

const persistConfig = {
  name: 'so-clover-storage', // Nom de la clé dans sessionStorage pour le store entier ou partitionné
  storage: createJSONStorage(() => sessionStorage),
  partialize: (state: GameState) => ({
    playerId: state.playerId,
    playerName: state.playerName,
    gameId: state.gameId
  }), // Persistance de l'identité et de la session de jeu
}

type GameStoreMutators = [['zustand/persist', unknown]]

export const useGameStore = create<GameState>()(
  (
    isDebug
      ? devtools(persist(gameStateCreator, persistConfig), { name: 'GameStore', enabled: true, serialize: { options: true } })
      : persist(gameStateCreator, persistConfig)
  ) as StateCreator<GameState, [], GameStoreMutators>
)

interface PresenceState {
  micePositions: Record<string, { x: number, y: number }>
  updateMousePosition: (playerId: string, x: number, y: number) => void
}

const presenceStoreDef: StateCreator<PresenceState, [["zustand/devtools", never]]> = (set) => ({
  micePositions: {},
  updateMousePosition: (playerId, x, y) => set((state) => ({
    micePositions: {
      ...state.micePositions,
      [playerId]: { x, y }
    }
  }), false, 'PresenceStore/updateMousePosition'),
})

export const usePresenceStore = create<PresenceState>()(
  (isDebug ? devtools(presenceStoreDef, { name: 'PresenceStore', enabled: true, serialize: { options: true } }) : presenceStoreDef) as StateCreator<PresenceState>
)

export const useNotificationStore = create<NotificationSlice>()(
  (isDebug
    ? devtools(createNotificationSlice, { name: 'NotificationStore', enabled: true, serialize: { options: true } })
    : createNotificationSlice
  ) as StateCreator<NotificationSlice>
)

export const useBoardStore = create<BoardSlice>()(
  (isDebug ? devtools(createBoardSlice, { name: 'BoardStore', enabled: true, serialize: { options: true } }) : createBoardSlice) as StateCreator<BoardSlice>
)

export const useGuessingStore = create<GuessingSlice>()(
  (isDebug ? devtools(createGuessingSlice, { name: 'GuessingStore', enabled: true, serialize: { options: true } }) : createGuessingSlice) as StateCreator<GuessingSlice>
)

export const useAppConfigStore = create<AppConfigSlice>()(
  (isDebug ? devtools(createAppConfigSlice, { name: 'AppConfigStore', enabled: true, serialize: { options: true } }) : createAppConfigSlice) as StateCreator<AppConfigSlice>
)
