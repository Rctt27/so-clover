import { useEffect, useRef, useMemo, useCallback } from 'react'
import { useGameStore, useGuessingStore } from '../../core/store'
import { shallow } from 'zustand/shallow'
import { useGameActions } from '../../hooks/useGameActions'
import { useNotifications } from '../../hooks/useNotifications'
import { useCardDrag } from '../../hooks/useCardDrag'
import { useDragOrchestration } from '../../hooks/useDragOrchestration'
import { OutsideCardPool } from './OutsideCardPool'
import { DraggableCard } from './DraggableCard'
import { GuessingBoardSection } from './GuessingBoardSection'
import { GuessingControls } from './GuessingControls'
import { CardData, rotationToDegrees, CardInfoResponse } from '../../types/game'
import { playSound } from '../../core/sounds'
import { debugLog } from '../../core/debug'

export const GuessingPage = () => {
  const { playerId } = useGameStore()
  const {
    currentBoardOwnerName,
    currentBoardOwnerId,
    outsideCards,
    guessedPositions,
    currentBoardClues,
    cumulativeBoardRotation,
    isValidationPending,
    remainingAttempts,
    correctlyPlacedPositions,
  } = useGuessingStore(
    (s) => ({
      currentBoardOwnerName: s.currentBoardOwnerName,
      currentBoardOwnerId: s.currentBoardOwnerId,
      outsideCards: s.outsideCards,
      guessedPositions: s.guessedPositions,
      currentBoardClues: s.currentBoardClues,
      cumulativeBoardRotation: s.cumulativeBoardRotation,
      isValidationPending: s.isValidationPending,
      remainingAttempts: s.remainingAttempts,
      correctlyPlacedPositions: s.correctlyPlacedPositions,
    }),
    shallow,
  )
  const setCumulativeBoardRotation = useGuessingStore((s) => s.setCumulativeBoardRotation)
  const resetGuessingState = useGuessingStore((s) => s.resetGuessingState)
  const {
    fetchGameState,
    loading,
    validateBoard,
    broadcastBoardRotation,
    nextBoard,
  } = useGameActions()
  const { notifyTopCenter } = useNotifications()

  const boardRef = useRef<HTMLDivElement>(null)
  const notifiedBoardId = useRef<string | null>(null)

  // ─── Derived state ──────────────────────────────────────────────────────────

  const isMyBoard = currentBoardOwnerId === playerId

  const safeCumulativeRotation =
    typeof cumulativeBoardRotation === 'number' && !isNaN(cumulativeBoardRotation)
      ? cumulativeBoardRotation
      : 0

  const isBoardFull = [
    guessedPositions['TopLeft'],
    guessedPositions['TopRight'],
    guessedPositions['BottomRight'],
    guessedPositions['BottomLeft'],
  ].every((c) => c !== null && c !== undefined)

  const isBoardGuessed = correctlyPlacedPositions.length === 4
  const canMoveToNext = isBoardGuessed || (remainingAttempts === 0 && !isValidationPending)

  // ─── Custom drag hooks ──────────────────────────────────────────────────────

  const { handleDragEnd, displacedSlot } = useDragOrchestration({
    outsideCards,
    correctlyPlacedPositions,
    fetchGameState,
  })

  const { dragState, createDragHandlers } = useCardDrag({
    boardRef,
    boardRotationDeg: safeCumulativeRotation,
    onDragEnd: handleDragEnd,
    disabled: isMyBoard || isValidationPending || canMoveToNext,
  })

  // ─── Memoized data ──────────────────────────────────────────────────────────

  const poolMapping = useMemo(() => {
    const mapping: Record<number, CardInfoResponse | null> = {
      0: null, 1: null, 2: null, 3: null, 4: null, 5: null,
    }
    outsideCards.forEach((card, i) => {
      if (i < 6) mapping[i] = card
    })
    return mapping
  }, [outsideCards])

  const poolLeft = useMemo(
    () => [poolMapping[0], poolMapping[1], poolMapping[2]],
    [poolMapping]
  )
  const poolRight = useMemo(
    () => [poolMapping[3], poolMapping[4], poolMapping[5]],
    [poolMapping]
  )

  const boardGuessedCards = useMemo<(CardInfoResponse | null)[]>(
    () => [
      guessedPositions['TopLeft'] ?? null,
      guessedPositions['TopRight'] ?? null,
      guessedPositions['BottomRight'] ?? null,
      guessedPositions['BottomLeft'] ?? null,
    ],
    [guessedPositions]
  )

  const boardCards = useMemo<(CardData | null)[]>(
    () =>
      (['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'] as const).map((pos) => {
        const g = guessedPositions[pos]
        if (!g) return null
        return {
          words: [g.topWord, g.rightWord, g.bottomWord, g.leftWord],
          rotation: rotationToDegrees(g.rotation),
        }
      }),
    [guessedPositions]
  )

  const clues = useMemo(
    () => ({
      top: currentBoardClues.find((c) => c.direction === 'Top')?.text || '',
      right: currentBoardClues.find((c) => c.direction === 'Right')?.text || '',
      bottom: currentBoardClues.find((c) => c.direction === 'Bottom')?.text || '',
      left: currentBoardClues.find((c) => c.direction === 'Left')?.text || '',
    }),
    [currentBoardClues]
  )

  // ─── Effects ────────────────────────────────────────────────────────────────

  // [DEBUG] Mount / Unmount
  useEffect(() => {
    debugLog('GuessingPage', 'MOUNTED');
    return () => {
      debugLog('GuessingPage', 'UNMOUNTED');
      resetGuessingState();
    }
  }, [resetGuessingState])

  useEffect(() => {
    fetchGameState()
  }, [fetchGameState])

  useEffect(() => {
    if (isMyBoard && currentBoardOwnerId && notifiedBoardId.current !== currentBoardOwnerId) {
      notifyTopCenter("C'est votre plateau ! Observez les autres joueurs.", { duration: 10000 })
      notifiedBoardId.current = currentBoardOwnerId
    } else if (!isMyBoard) {
      notifiedBoardId.current = null
    }
  }, [isMyBoard, currentBoardOwnerId, notifyTopCenter])

  // ─── Handlers ───────────────────────────────────────────────────────────────

  const handleRotateBoard = useCallback((direction: 'left' | 'right') => {
    if (isMyBoard || isValidationPending) return
    const rotationDelta = direction === 'right' ? 90 : -90
    const newRotation = safeCumulativeRotation + rotationDelta
    playSound('boardRotate')
    setCumulativeBoardRotation(newRotation)
    broadcastBoardRotation(newRotation)
  }, [isMyBoard, isValidationPending, safeCumulativeRotation, setCumulativeBoardRotation, broadcastBoardRotation])

  // Find the card being dragged for the overlay
  const findDraggedCard = useCallback((): CardInfoResponse | null => {
    const cardId = dragState.draggedCardId
    if (!cardId) return null

    // Search board positions
    for (const pos of ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'] as const) {
      const c = guessedPositions[pos]
      if (c?.cardId === cardId) return c
    }
    // Search pool
    for (const card of outsideCards) {
      if (card?.cardId === cardId) return card
    }
    return null
  }, [dragState.draggedCardId, guessedPositions, outsideCards])

  // ─── Loading state ──────────────────────────────────────────────────────────

  if (loading && !currentBoardOwnerName) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500 text-lg">Préparation du plateau...</p>
      </div>
    )
  }

  // Only pass highlightedSlot for pool slot ids
  const poolHighlightedSlot =
    dragState.targetSlot?.startsWith('pool-') ? dragState.targetSlot : null

  return (
    <div className="flex flex-col min-h-screen">
      {/* Header Info */}
      <div className="bg-white/30 backdrop-blur-sm shadow-sm p-4 text-center">
        <h1 className="text-2xl font-bold text-gray-800">Phase de Déduction</h1>
        <p className="text-gray-600">
          Plateau de{' '}
          <span className="font-bold text-clover-dark">{currentBoardOwnerName}</span>
        </p>
      </div>

      <div className="flex flex-1 items-center justify-between px-8 py-4 gap-8 overflow-hidden">
        {/* Pool Gauche */}
        <div className="flex-none">
          <OutsideCardPool
            cards={poolLeft}
            startIndex={0}
            disabled={isMyBoard || canMoveToNext}
            displacedSlot={displacedSlot}
            highlightedSlot={poolHighlightedSlot}
            dragHandlers={isMyBoard || isValidationPending || canMoveToNext ? undefined : createDragHandlers}
            dragSourceCardId={dragState.draggedCardId}
          />
        </div>

        {/* Board Central */}
        <div className="flex-1 flex flex-col items-center justify-center gap-8 min-w-0 max-w-[1000px]">
          <GuessingBoardSection
            boardRef={boardRef}
            boardCards={boardCards}
            boardGuessedCards={boardGuessedCards}
            clues={clues}
            rotation={safeCumulativeRotation}
            currentBoardOwnerId={currentBoardOwnerId}
            isMyBoard={isMyBoard}
            isValidationPending={isValidationPending}
            canMoveToNext={canMoveToNext}
            correctlyPlacedPositions={correctlyPlacedPositions}
            displacedSlot={displacedSlot}
            dragState={dragState}
            createDragHandlers={createDragHandlers}
          />

          <GuessingControls
            isMyBoard={isMyBoard}
            isValidationPending={isValidationPending}
            isBoardFull={isBoardFull}
            isBoardGuessed={isBoardGuessed}
            canMoveToNext={canMoveToNext}
            remainingAttempts={remainingAttempts}
            onValidate={validateBoard}
            onNextBoard={nextBoard}
            rotation={safeCumulativeRotation}
            onRotate={handleRotateBoard}
          />
        </div>

        {/* Pool Droit */}
        <div className="flex-none">
          <OutsideCardPool
            cards={poolRight}
            startIndex={3}
            disabled={isMyBoard || canMoveToNext}
            displacedSlot={displacedSlot}
            highlightedSlot={poolHighlightedSlot}
            dragHandlers={isMyBoard || isValidationPending || canMoveToNext ? undefined : createDragHandlers}
            dragSourceCardId={dragState.draggedCardId}
          />
        </div>
      </div>

      {/* Drag Overlay */}
      {dragState.isDragging && dragState.draggedCardId && (() => {
        const draggedCard = findDraggedCard()
        if (!draggedCard) return null

        const isFromOutside = outsideCards.some((c) => c?.cardId === draggedCard.cardId)
        return (
          <div
            className="fixed pointer-events-none z-[1000]"
            style={{
              left: dragState.dragPosition.x,
              top: dragState.dragPosition.y,
              transform: 'translate(-50%, -50%)',
              width: 180,
              height: 180,
            }}
          >
            <DraggableCard
              card={draggedCard}
              index={isFromOutside ? outsideCards.findIndex((c) => c?.cardId === draggedCard.cardId) : 0}
              isOutside={isFromOutside}
              disabled={true}
              isSelected={true}
              dragRotationOverride={isFromOutside ? 0 : -safeCumulativeRotation}
            />
          </div>
        )
      })()}
    </div>
  )
}
