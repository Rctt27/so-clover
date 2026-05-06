import { StateCreator } from 'zustand'
import { CardInfoResponse, ClueInfoResponse } from '../../types/game'

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
  lastLocalRotationTimestamp: number // Timestamp of last local rotation to prevent race conditions
  lastAppliedRotationRevision: number
  isValidationPending: boolean
  validationResults: { correctPositions: string[], incorrectPositions: string[] } | null

  // Actions
  setGuessingState: (state: Partial<GuessingSlice>) => void
  setCumulativeBoardRotation: (rotation: number, isLocalChange?: boolean) => void
  /**
   * Apply a server rotation only if its revision is strictly greater than what we already applied.
   * Returns true if the update was applied.
   */
  applyServerRotation: (rotation: number, revision: number) => boolean
  setIsValidationPending: (pending: boolean) => void
  setValidationResults: (results: { correctPositions: string[], incorrectPositions: string[] } | null) => void
  setSelectedCardId: (id: string | null) => void
  resetGuessingState: () => void
}

export const createGuessingSlice: StateCreator<GuessingSlice, [["zustand/devtools", never]]> = (set) => ({
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
  lastLocalRotationTimestamp: 0,
  lastAppliedRotationRevision: 0,
  isValidationPending: false,
  validationResults: null,

  setGuessingState: (state) => set((prev) => {
    // Protection supplémentaire : toujours préserver cumulativeBoardRotation
    // sauf si explicitement fournie dans l'update
    const nextState = { ...prev, ...state };
    if (!('cumulativeBoardRotation' in state)) {
      nextState.cumulativeBoardRotation = prev.cumulativeBoardRotation;
    }
    return nextState;
  }, false, 'GuessingStore/setGuessingState'),
  setCumulativeBoardRotation: (rotation, isLocalChange = false) => set({
    cumulativeBoardRotation: rotation,
    ...(isLocalChange ? { lastLocalRotationTimestamp: Date.now() } : {})
  }, false, 'GuessingStore/setCumulativeBoardRotation'),
  applyServerRotation: (rotation, revision) => {
    let applied = false
    set((state) => {
      if (revision <= state.lastAppliedRotationRevision) return state
      applied = true
      return {
        cumulativeBoardRotation: rotation,
        lastAppliedRotationRevision: revision,
      }
    }, false, 'GuessingStore/applyServerRotation')
    return applied
  },
  setIsValidationPending: (pending) => set({ isValidationPending: pending }, false, 'GuessingStore/setIsValidationPending'),
  setValidationResults: (results) => set({ validationResults: results }, false, 'GuessingStore/setValidationResults'),
  setSelectedCardId: (id) => set({ selectedCardId: id }, false, 'GuessingStore/setSelectedCardId'),
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
    lastLocalRotationTimestamp: 0,
    lastAppliedRotationRevision: 0,
    isValidationPending: false,
    validationResults: null,
  }, false, 'GuessingStore/resetGuessingState'),
})
