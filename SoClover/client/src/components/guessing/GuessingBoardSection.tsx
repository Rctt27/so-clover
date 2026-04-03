import React from 'react'
import { Board } from '../shared/Board'
import {
  RemoteCursorsLayer,
  useLocalCursorEmitter,
  useMouseTrackingEnabled,
} from '../../features/mouseTracking'
import { CardData, CardInfoResponse } from '../../types/game'
import { DragState } from '../../hooks/useCardDrag'

export interface GuessingBoardSectionProps {
  boardRef: React.RefObject<HTMLDivElement>
  boardCards: (CardData | null)[]
  boardGuessedCards: (CardInfoResponse | null)[]
  clues: { top: string; right: string; bottom: string; left: string }
  rotation: number
  currentBoardOwnerId: string | null
  isMyBoard: boolean
  isValidationPending: boolean
  canMoveToNext: boolean
  correctlyPlacedPositions: string[]
  displacedSlot: string | null
  dragState: DragState
  createDragHandlers: (slotId: string, cardId: string) => { onPointerDown: (e: React.PointerEvent) => void }
}

export const GuessingBoardSection = React.memo(({
  boardRef,
  boardCards,
  boardGuessedCards,
  clues,
  rotation,
  currentBoardOwnerId,
  isMyBoard,
  isValidationPending,
  canMoveToNext,
  correctlyPlacedPositions,
  displacedSlot,
  dragState,
  createDragHandlers,
}: GuessingBoardSectionProps) => {
  const isMouseTrackingEnabled = useMouseTrackingEnabled()

  // Activer l'émission locale seulement si pas owner du board
  useLocalCursorEmitter(boardRef, isMouseTrackingEnabled && !isMyBoard)

  const isDraggingDisabled = isMyBoard || isValidationPending || canMoveToNext

  // Only pass highlightedSlot for board slot ids (not pool slots)
  const boardHighlightedSlot =
    dragState.targetSlot && !dragState.targetSlot.startsWith('pool-')
      ? dragState.targetSlot
      : null

  return (
    <div className="relative">
      <Board
        ref={boardRef}
        key={currentBoardOwnerId || 'no-board'}
        cards={boardCards}
        guessedCards={boardGuessedCards}
        displacedSlot={displacedSlot}
        rotation={rotation}
        clues={clues}
        animateEntry={true}
        showClueInputs={false}
        disabled={isDraggingDisabled}
        isLocked={isDraggingDisabled}
        correctPositions={correctlyPlacedPositions}
        ownerId={currentBoardOwnerId || undefined}
        highlightedSlot={boardHighlightedSlot}
        dragHandlers={isDraggingDisabled ? undefined : createDragHandlers}
        dragSourceCardId={dragState.draggedCardId}
        dragTargetSlot={dragState.targetSlot}
      />

      {/* Remote Cursors Layer — rendered inside board bounds */}
      <RemoteCursorsLayer
        boardRef={boardRef}
        enabled={isMouseTrackingEnabled}
      />
    </div>
  )
});
GuessingBoardSection.displayName = 'GuessingBoardSection';
