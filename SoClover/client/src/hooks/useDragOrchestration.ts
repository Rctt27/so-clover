import { useState, useCallback, useRef, useEffect } from 'react'
import { CardInfoResponse } from '../types/game'
import { LOGICAL_SLOTS } from '../core/utils'
import { useGameActions } from './useGameActions'
import { playSound } from '../core/sounds'

const SWAP_ANIMATION_DURATION_MS = 500

// Board positions set for fast membership checks
const BOARD_POSITIONS = new Set<string>(LOGICAL_SLOTS)

/**
 * Parse a slot string and determine if it belongs to the board or the pool.
 * Board slots: 'TopLeft' | 'TopRight' | 'BottomRight' | 'BottomLeft'
 * Pool slots: 'pool-0' … 'pool-5'
 */
function isPoolSlot(slot: string): boolean {
  return slot.startsWith('pool-')
}

function isBoardSlot(slot: string): boolean {
  return BOARD_POSITIONS.has(slot)
}

function parsePoolIndex(slot: string): number {
  return parseInt(slot.replace('pool-', ''), 10)
}

// ---------------------------------------------------------------------------

export interface UseDragOrchestrationDeps {
  outsideCards: (CardInfoResponse | null)[]
  correctlyPlacedPositions: string[]
  fetchGameState: () => Promise<unknown>
}

export interface UseDragOrchestrationReturn {
  handleDragEnd: (sourceSlot: string, targetSlot: string) => Promise<void>
  displacedSlot: string | null
  /** Incrémenté à chaque drag impliquant le board. Réservé pour les animations Story 7. */
  swapAnimationKey: number
}

/**
 * useDragOrchestration
 *
 * Encapsulates all drag business logic for the guessing phase:
 * - Determines drag type from (sourceSlot, targetSlot) strings
 * - Dispatches the correct API call for each drag type
 * - Manages swap animation state (displacedSlot, swapAnimationKey)
 * - Detects pool desyncs and triggers a state refresh
 * - Prevents actions on locked (correctly placed) slots
 * - Cleans up pending timeouts on unmount
 */
export function useDragOrchestration(deps: UseDragOrchestrationDeps): UseDragOrchestrationReturn {
  const {
    outsideCards,
    correctlyPlacedPositions,
    fetchGameState,
  } = deps

  const { placeGuessingCard, swapGuessingCards, swapOutsidePoolCards, returnGuessingCard } =
    useGameActions()

  const [displacedSlot, setDisplacedSlot] = useState<string | null>(null)
  const [swapAnimationKey, setSwapAnimationKey] = useState(0)

  // Track pending timeouts so we can clear them on unmount
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (timeoutRef.current !== null) {
        clearTimeout(timeoutRef.current)
      }
    }
  }, [])

  // Schedule a reset of displacedSlot after the animation window
  const scheduleAnimationReset = useCallback(() => {
    if (timeoutRef.current !== null) {
      clearTimeout(timeoutRef.current)
    }
    timeoutRef.current = setTimeout(() => {
      setDisplacedSlot(null)
      timeoutRef.current = null
    }, SWAP_ANIMATION_DURATION_MS)
  }, [])

  const pendingRef = useRef(false)

  const handleDragEnd = useCallback(
    async (sourceSlot: string, targetSlot: string): Promise<void> => {
      if (pendingRef.current) return
      pendingRef.current = true
      try {
        // No-op if source and target are the same
        if (sourceSlot === targetSlot) return

        const fromBoard = isBoardSlot(sourceSlot)
        const fromPool = isPoolSlot(sourceSlot)
        const toBoard = isBoardSlot(targetSlot)
        const toPool = isPoolSlot(targetSlot)

        // Guard: slot must be recognised
        if ((!fromBoard && !fromPool) || (!toBoard && !toPool)) {
          console.warn('[useDragOrchestration] Unrecognised slot pair:', sourceSlot, '→', targetSlot)
          return
        }

        // Guard: prevent interactions with correctly placed (locked) board slots
        if (fromBoard && correctlyPlacedPositions.includes(sourceSlot)) return
        if (toBoard && correctlyPlacedPositions.includes(targetSlot)) return

        // -----------------------------------------------------------------------
        // Case 1: Board → Board (swap two guessed positions)
        // -----------------------------------------------------------------------
        if (fromBoard && toBoard) {
          setDisplacedSlot(targetSlot)
          setSwapAnimationKey((k) => k + 1)
          await swapGuessingCards(sourceSlot, targetSlot)
          playSound('cardSwap')
          scheduleAnimationReset()
          return
        }

        // -----------------------------------------------------------------------
        // Case 2: Pool → Board (place a card from the pool onto the board)
        // -----------------------------------------------------------------------
        if (fromPool && toBoard) {
          const poolIndex = parsePoolIndex(sourceSlot)

          if (isNaN(poolIndex) || poolIndex < 0 || poolIndex > 5) {
            console.warn('[useDragOrchestration] Invalid pool index from slot:', sourceSlot)
            return
          }

          const cardAtIndex = outsideCards[poolIndex]
          if (cardAtIndex) {
            setDisplacedSlot(targetSlot)
            setSwapAnimationKey((k) => k + 1)
            await placeGuessingCard(poolIndex, targetSlot)
            playSound('cardPlace')
            scheduleAnimationReset()
          } else {
            console.warn('[useDragOrchestration] Pool desync detected at index', poolIndex, '— refreshing state')
            await fetchGameState()
          }
          return
        }

        // -----------------------------------------------------------------------
        // Case 3: Board → Pool (return a card from the board to the pool)
        // -----------------------------------------------------------------------
        if (fromBoard && toPool) {
          setDisplacedSlot(sourceSlot)
          setSwapAnimationKey((k) => k + 1)
          await returnGuessingCard(sourceSlot)
          playSound('cardPlace')
          scheduleAnimationReset()
          return
        }

        // -----------------------------------------------------------------------
        // Case 4: Pool → Pool (swap two pool slots)
        // -----------------------------------------------------------------------
        if (fromPool && toPool) {
          const sIdx = parsePoolIndex(sourceSlot)
          const tIdx = parsePoolIndex(targetSlot)

          if (isNaN(sIdx) || isNaN(tIdx) || sIdx === tIdx || sIdx < 0 || sIdx > 5 || tIdx < 0 || tIdx > 5) return

          // No optimistic update — let SignalR synchronise outsideCards via the store
          await swapOutsidePoolCards(sIdx, tIdx)
          playSound('cardSwap')
        }
      } finally {
        pendingRef.current = false
      }
    },
    [
      outsideCards,
      correctlyPlacedPositions,
      fetchGameState,
      placeGuessingCard,
      swapGuessingCards,
      swapOutsidePoolCards,
      returnGuessingCard,
      scheduleAnimationReset,
    ],
  )

  return { handleDragEnd, displacedSlot, swapAnimationKey }
}
