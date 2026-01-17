import { StateCreator } from 'zustand'
import { CardInfoResponse, ClueInfoResponse } from '../types/game'

export interface GuessingSlice {
  // État provenant du serveur
  currentBoardOwnerId: string | null
  currentBoardOwnerName: string | null
  outsideCards: (CardInfoResponse | null)[]
  guessedPositions: Record<string, CardInfoResponse | null>
  correctlyPlacedPositions: string[]
  remainingAttempts: number
  currentBoardClues: ClueInfoResponse[]

  // État local (UI)
  selectedCardId: string | null // Pour le mode tablette / clic-clic
  cumulativeBoardRotation: number
  isValidationPending: boolean
  validationResults: { correctPositions: string[], incorrectPositions: string[] } | null

  // Actions
  setGuessingState: (state: Partial<GuessingSlice>) => void
  setCumulativeBoardRotation: (rotation: number) => void
  setIsValidationPending: (pending: boolean) => void
  setValidationResults: (results: { correctPositions: string[], incorrectPositions: string[] } | null) => void
  setSelectedCardId: (id: string | null) => void
  resetGuessingState: () => void
}

export const createGuessingSlice: StateCreator<GuessingSlice> = (set) => ({
  currentBoardOwnerId: null,
  currentBoardOwnerName: null,
  outsideCards: [],
  guessedPositions: {
    'TopLeft': null,
    'TopRight': null,
    'BottomLeft': null,
    'BottomRight': null,
  },
  correctlyPlacedPositions: [],
  remainingAttempts: 0,
  currentBoardClues: [],

  selectedCardId: null,
  cumulativeBoardRotation: 0,
  isValidationPending: false,
  validationResults: null,

  setGuessingState: (state) => set((prev) => ({ ...prev, ...state })),
  setCumulativeBoardRotation: (rotation) => set({ cumulativeBoardRotation: rotation }),
  setIsValidationPending: (pending) => set({ isValidationPending: pending }),
  setValidationResults: (results) => set({ validationResults: results }),
  setSelectedCardId: (id) => set({ selectedCardId: id }),
  resetGuessingState: () => set({
    currentBoardOwnerId: null,
    currentBoardOwnerName: null,
    outsideCards: [],
    guessedPositions: {
      'TopLeft': null,
      'TopRight': null,
      'BottomLeft': null,
      'BottomRight': null,
    },
    correctlyPlacedPositions: [],
    remainingAttempts: 0,
    currentBoardClues: [],
    selectedCardId: null,
    cumulativeBoardRotation: 0,
    isValidationPending: false,
    validationResults: null,
  }),
})
