import { create, StateCreator } from 'zustand'
import { persist, createJSONStorage, devtools } from 'zustand/middleware'
import { Role, GamePhase, ConnectionStatus } from '../types/game'
import { createNotificationSlice, NotificationSlice } from './notificationSlice'
import { createBoardSlice, BoardSlice } from './boardSlice'
import { createGuessingSlice, GuessingSlice } from './guessingSlice'
import { isDebug } from './debug'

interface GameState {
  gameId: string | null
  playerId: string | null
  playerName: string | null
  phase: GamePhase
  role: Role
  isGameAdmin: boolean
  connectionStatus: ConnectionStatus
  players: Array<{ playerId: string, name: string, cursorColorIndex: number }>
  isInitializing: boolean
  phaseEndsAtUtc: string | null
  settings: {
    language: string
    cluesDurationSeconds: number
    guessDurationSeconds: number
  }
  setPhase: (phase: GamePhase) => void
  setRole: (role: Role) => void
  setIsGameAdmin: (isAdmin: boolean) => void
  setGameId: (gameId: string | null) => void
  setPlayerId: (playerId: string | null) => void
  setPlayerName: (playerName: string | null) => void
  setConnectionStatus: (status: ConnectionStatus) => void
  setPlayers: (players: Array<{ playerId: string, name: string, cursorColorIndex: number }>) => void
  setPhaseEndsAtUtc: (deadline: string | null) => void
  setSettings: (settings: { language: string, cluesDurationSeconds: number, guessDurationSeconds: number }) => void
  setIsInitializing: (isInitializing: boolean) => void
  resetAuth: () => void
}

const gameStateCreator: StateCreator<GameState> = (set) => ({
  gameId: null,
  playerId: null,
  playerName: null,
  phase: 'Initial',
  role: 'PlayerWritingClue',
  isGameAdmin: false,
  isInitializing: false,
  connectionStatus: 'Disconnected',
  players: [],
  phaseEndsAtUtc: null,
  settings: {
    language: 'Français_OFF',
    cluesDurationSeconds: 300,
    guessDurationSeconds: 300
  },
  setPhase: (phase) => set({ phase }),
  setRole: (role) => set({ role }),
  setIsGameAdmin: (isGameAdmin) => set({ isGameAdmin }),
  setGameId: (gameId) => set({ gameId }),
  setPlayerId: (playerId) => set({ playerId }),
  setPlayerName: (playerName) => set({ playerName }),
  setConnectionStatus: (status) => set({ connectionStatus: status }),
  setPlayers: (players) => set({ players }),
  setPhaseEndsAtUtc: (phaseEndsAtUtc) => set({ phaseEndsAtUtc }),
  setSettings: (settings) => set({ settings }),
  setIsInitializing: (isInitializing) => set({ isInitializing }),
  resetAuth: () => set({
    playerId: null,
    playerName: null,
    gameId: null,
    role: 'PlayerWritingClue',
    isGameAdmin: false,
    players: [],
    phase: 'Initial',
    phaseEndsAtUtc: null,
    settings: {
      language: 'Français_OFF',
      cluesDurationSeconds: 300,
      guessDurationSeconds: 300
    }
  }),
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
      ? devtools(persist(gameStateCreator, persistConfig), { name: 'GameStore' })
      : persist(gameStateCreator, persistConfig)
  ) as StateCreator<GameState, [], GameStoreMutators>
)

interface PresenceState {
  micePositions: Record<string, { x: number, y: number }>
  updateMousePosition: (playerId: string, x: number, y: number) => void
}

const presenceStoreDef: StateCreator<PresenceState> = (set) => ({
  micePositions: {},
  updateMousePosition: (playerId, x, y) => set((state) => ({
    micePositions: {
      ...state.micePositions,
      [playerId]: { x, y }
    }
  })),
})

export const usePresenceStore = create<PresenceState>()(
  (isDebug ? devtools(presenceStoreDef, { name: 'PresenceStore' }) : presenceStoreDef) as StateCreator<PresenceState>
)

/**
 * @deprecated Use useNotifications hook instead of direct store access
 */
export const useNotificationStore = create<NotificationSlice>()(
  createNotificationSlice
)

export const useBoardStore = create<BoardSlice>()(
  (isDebug ? devtools(createBoardSlice, { name: 'BoardStore' }) : createBoardSlice) as StateCreator<BoardSlice>
)

export const useGuessingStore = create<GuessingSlice>()(
  (isDebug ? devtools(createGuessingSlice, { name: 'GuessingStore' }) : createGuessingSlice) as StateCreator<GuessingSlice>
)
