import { StateCreator } from 'zustand'
import { BoardData, CardData } from '../types/game'
import { isDebug } from './debug'

export interface BoardSlice {
  // Board state
  myBoard: BoardData | null
  otherBoards: Record<string, BoardData> // Key: playerId
  currentBoardOwner: string | null // PlayerId of the board currently being guessed

  // Actions
  setMyBoard: (board: BoardData) => void
  updateMyBoardCards: (cards: (CardData | null)[]) => void
  updateMyBoardRotation: (rotation: number) => void
  updateMyClue: (position: 'top' | 'right' | 'bottom' | 'left', text: string) => void
  setMyBoardSubmitted: (isSubmitted: boolean) => void
  setOtherBoard: (playerId: string, board: BoardData) => void
  setCurrentBoardOwner: (playerId: string | null) => void
  resetBoards: () => void
}

export const createBoardSlice: StateCreator<BoardSlice> = (set) => ({
  myBoard: null,
  otherBoards: {},
  currentBoardOwner: null,

  setMyBoard: (board) => {
    if (isDebug) console.log('[boardSlice] setMyBoard called with:', board)
    set({ myBoard: board })
  },

  updateMyBoardCards: (cards) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        cards
      }
    }
  }),

  updateMyBoardRotation: (rotation) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        rotation
      }
    }
  }),

  updateMyClue: (position, text) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        clues: {
          ...state.myBoard.clues,
          [position]: {
            text,
            playerId: state.myBoard.clues[position].playerId
          }
        }
      }
    }
  }),
  
  setMyBoardSubmitted: (isSubmitted) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        isSubmitted
      }
    }
  }),

  setOtherBoard: (playerId, board) => set((state) => ({
    otherBoards: {
      ...state.otherBoards,
      [playerId]: board
    }
  })),

  setCurrentBoardOwner: (playerId) => set({ currentBoardOwner: playerId }),

  resetBoards: () => set({
    myBoard: null,
    otherBoards: {},
    currentBoardOwner: null
  })
})
