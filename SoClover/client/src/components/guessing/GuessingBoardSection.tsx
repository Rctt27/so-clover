import React from 'react'
import { Board } from '../shared/board/Board'
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
  clueExplanations?: { top: string | null; right: string | null; bottom: string | null; left: string | null }
  rotation: number
  currentBoardOwnerId: string | null
  isMyBoard: boolean
  isValidationPending: boolean
  canMoveToNext: boolean
  correctlyPlacedPositions: string[]
  revealedCards: (CardInfoResponse | null)[]
  displacedSlot: string | null
  dragState: DragState
  createDragHandlers: (slotId: string, cardId: string) => { onPointerDown: (e: React.PointerEvent) => void }
  onSlotClick?: (slotId: string) => void
  selectedSlot?: string | null
}

export const GuessingBoardSection = React.memo(({
  boardRef,
  boardCards,
  boardGuessedCards,
  clues,
  clueExplanations,
  rotation,
  currentBoardOwnerId,
  isMyBoard,
  isValidationPending,
  canMoveToNext,
  correctlyPlacedPositions,
  revealedCards,
  displacedSlot,
  dragState,
  createDragHandlers,
  onSlotClick,
  selectedSlot,
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
    <div className="relative w-full flex justify-center">
      <Board
        ref={boardRef}
        key={currentBoardOwnerId || 'no-board'}
        cards={boardCards}
        guessedCards={boardGuessedCards}
        displacedSlot={displacedSlot}
        rotation={rotation}
        clues={clues}
        clueExplanations={clueExplanations}
        animateEntry={true}
        showClueInputs={false}
        containerSized
        disabled={isDraggingDisabled}
        isLocked={isDraggingDisabled}
        correctPositions={correctlyPlacedPositions}
        revealedCards={revealedCards}
        ownerId={currentBoardOwnerId || undefined}
        highlightedSlot={boardHighlightedSlot}
        dragHandlers={isDraggingDisabled ? undefined : createDragHandlers}
        onSlotClick={isDraggingDisabled ? undefined : onSlotClick}
        selectedSlot={selectedSlot}
        dragSourceCardId={dragState.draggedCardId}
        dragSourceSlot={dragState.sourceSlot}
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
