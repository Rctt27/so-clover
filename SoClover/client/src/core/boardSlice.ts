import { StateCreator } from 'zustand'
import { BoardData, CardData } from '../types/game'
import { debugLog } from './debug'

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

export const createBoardSlice: StateCreator<BoardSlice, [["zustand/devtools", never]]> = (set) => ({
  myBoard: null,
  otherBoards: {},
  currentBoardOwner: null,

  setMyBoard: (board) => {
    debugLog('boardSlice', 'setMyBoard called with:', board)
    set({ myBoard: board }, false, 'BoardStore/setMyBoard')
  },

  updateMyBoardCards: (cards) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        cards
      }
    }
  }, false, 'BoardStore/updateMyBoardCards'),

  updateMyBoardRotation: (rotation) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        rotation
      }
    }
  }, false, 'BoardStore/updateMyBoardRotation'),

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
  }, false, 'BoardStore/updateMyClue'),
  
  setMyBoardSubmitted: (isSubmitted) => set((state) => {
    if (!state.myBoard) return state
    return {
      myBoard: {
        ...state.myBoard,
        isSubmitted
      }
    }
  }, false, 'BoardStore/setMyBoardSubmitted'),

  setOtherBoard: (playerId, board) => set((state) => ({
    otherBoards: {
      ...state.otherBoards,
      [playerId]: board
    }
  }), false, 'BoardStore/setOtherBoard'),

  setCurrentBoardOwner: (playerId) => set({ currentBoardOwner: playerId }, false, 'BoardStore/setCurrentBoardOwner'),

  resetBoards: () => set({
    myBoard: null,
    otherBoards: {},
    currentBoardOwner: null
  }, false, 'BoardStore/resetBoards')
})
