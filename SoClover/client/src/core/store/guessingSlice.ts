import { StateCreator } from 'zustand'
import { CardInfoResponse, ClueInfoResponse, FailedPlacementInfo } from '../../types/game'

export interface GuessingSlice {
  // État provenant du serveur
  currentBoardOwnerId: string | null
  currentBoardOwnerName: string | null
  outsideCards: (CardInfoResponse | null)[]
  guessedPositions: Record<string, CardInfoResponse | null>
  correctlyPlacedPositions: string[]
  remainingAttempts: number
  currentBoardClues: ClueInfoResponse[]
  failedPlacements: FailedPlacementInfo[]
  solution: Record<string, CardInfoResponse | null> | null

  // État local (UI)
  selectedSlotId: string | null // Slot sélectionné pour le mode clic-clic (tablette) : 'pool-N' ou slot logique board
  cumulativeBoardRotation: number
  lastAppliedRotationRevision: number
  isValidationPending: boolean
  validationResults: { correctPositions: string[], incorrectPositions: string[] } | null
  /**
   * True pendant un drag local (déplacement OU rotation) et durant la fenêtre
   * post-drop (≈500 ms) le temps que le SignalR roundtrip se propage. Sert à
   * supprimer les animations de transition de cartes sur le UI de l'initiateur,
   * puisqu'il a déjà vu le mouvement via le drag. Les autres clients ne
   * touchent jamais à ce flag, donc ils continuent à animer normalement.
   */
  isLocalDragInProgress: boolean

  // Actions
  setGuessingState: (state: Partial<GuessingSlice>) => void
  setLocalDragActive: (active: boolean) => void
  setCumulativeBoardRotation: (rotation: number) => void
  /**
   * Apply a server rotation only if its revision is strictly greater than what we already applied.
   * Returns true if the update was applied.
   */
  applyServerRotation: (rotation: number, revision: number) => boolean
  setIsValidationPending: (pending: boolean) => void
  setValidationResults: (results: { correctPositions: string[], incorrectPositions: string[] } | null) => void
  setSelectedSlotId: (id: string | null) => void
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
  failedPlacements: [],
  solution: null,

  selectedSlotId: null,
  cumulativeBoardRotation: 0,
  lastAppliedRotationRevision: 0,
  isValidationPending: false,
  validationResults: null,
  isLocalDragInProgress: false,

  setGuessingState: (state) => set((prev) => {
    // Protection supplémentaire : toujours préserver cumulativeBoardRotation
    // sauf si explicitement fournie dans l'update
    const nextState = { ...prev, ...state };
    if (!('cumulativeBoardRotation' in state)) {
      nextState.cumulativeBoardRotation = prev.cumulativeBoardRotation;
    }
    return nextState;
  }, false, 'GuessingStore/setGuessingState'),
  setCumulativeBoardRotation: (rotation) => set({
    cumulativeBoardRotation: rotation,
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
  setLocalDragActive: (active) => set({ isLocalDragInProgress: active }, false, 'GuessingStore/setLocalDragActive'),
  setIsValidationPending: (pending) => set({ isValidationPending: pending }, false, 'GuessingStore/setIsValidationPending'),
  setValidationResults: (results) => set({ validationResults: results }, false, 'GuessingStore/setValidationResults'),
  setSelectedSlotId: (id) => set({ selectedSlotId: id }, false, 'GuessingStore/setSelectedSlotId'),
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
    failedPlacements: [],
    solution: null,
    selectedSlotId: null,
    cumulativeBoardRotation: 0,
    lastAppliedRotationRevision: 0,
    isValidationPending: false,
    validationResults: null,
    isLocalDragInProgress: false,
  }, false, 'GuessingStore/resetGuessingState'),
})
