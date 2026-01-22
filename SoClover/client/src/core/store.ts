import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import { Role, GamePhase, ConnectionStatus } from '../types/game'
import { createNotificationSlice, NotificationSlice } from './notificationSlice'
import { createBoardSlice, BoardSlice } from './boardSlice'
import { createGuessingSlice, GuessingSlice } from './guessingSlice'

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

export const useGameStore = create<GameState>()(
  persist(
    (set) => ({
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
    }),
    {
      name: 'so-clover-storage', // Nom de la clé dans sessionStorage pour le store entier ou partitionné
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({ 
        playerId: state.playerId, 
        playerName: state.playerName,
        gameId: state.gameId
      }), // Persistance de l'identité et de la session de jeu
    }
  )
)

interface PresenceState {
  micePositions: Record<string, { x: number, y: number }>
  updateMousePosition: (playerId: string, x: number, y: number) => void
}

export const usePresenceStore = create<PresenceState>((set) => ({
  micePositions: {},
  updateMousePosition: (playerId, x, y) => set((state) => ({
    micePositions: {
      ...state.micePositions,
      [playerId]: { x, y }
    }
  })),
}))

/**
 * @deprecated Use useNotifications hook instead of direct store access
 */
export const useNotificationStore = create<NotificationSlice>()(
  createNotificationSlice
)

export const useBoardStore = create<BoardSlice>()(
  createBoardSlice
)

export const useGuessingStore = create<GuessingSlice>()(
  createGuessingSlice
)
