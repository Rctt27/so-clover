import { useEffect, useRef, useMemo, useCallback, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useGameStore, useGuessingStore } from '../../core/store'
import { shallow } from 'zustand/shallow'
import { useGameActions } from '../../hooks/useGameActions'
import { useNotifications } from '../../hooks/useNotifications'
import { useCoarsePointer } from '../../hooks/useCoarsePointer'
import { useCardDrag } from '../../hooks/useCardDrag'
import { useDragOrchestration } from '../../hooks/useDragOrchestration'
import { OutsideCardPool } from './OutsideCardPool'
import { DraggableCard } from './DraggableCard'
import { GuessingBoardSection } from './GuessingBoardSection'
import { GuessingControls } from './GuessingControls'
import { LandscapePrompt } from './LandscapePrompt'
import { CardData, rotationToDegrees, CardInfoResponse } from '../../types/game'
import { playSound } from '../../core/sounds'
import { debugLog } from '../../core/debug'
import { isPlacementAlreadyTried } from '../../core/isPlacementAlreadyTried'
import { computeRevealedCards } from '../../core/computeRevealedCards'
import { CONSTANTS } from '../../core/constants'
import i18n from '../../i18n'

export const GuessingPage = () => {
  const { t } = useTranslation('guessing')
  const { playerId } = useGameStore()
  const {
    currentBoardOwnerId,
    outsideCards,
    guessedPositions,
    currentBoardClues,
    cumulativeBoardRotation,
    isValidationPending,
    remainingAttempts,
    correctlyPlacedPositions,
    failedPlacements,
    solution,
  } = useGuessingStore(
    (s) => ({
      currentBoardOwnerId: s.currentBoardOwnerId,
      outsideCards: s.outsideCards,
      guessedPositions: s.guessedPositions,
      currentBoardClues: s.currentBoardClues,
      cumulativeBoardRotation: s.cumulativeBoardRotation,
      isValidationPending: s.isValidationPending,
      remainingAttempts: s.remainingAttempts,
      correctlyPlacedPositions: s.correctlyPlacedPositions,
      failedPlacements: s.failedPlacements,
      solution: s.solution,
    }),
    shallow,
  )
  const setCumulativeBoardRotation = useGuessingStore((s) => s.setCumulativeBoardRotation)
  const resetGuessingState = useGuessingStore((s) => s.resetGuessingState)
  const selectedSlotId = useGuessingStore((s) => s.selectedSlotId)
  const setSelectedSlotId = useGuessingStore((s) => s.setSelectedSlotId)
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

  const isCoarse = useCoarsePointer()
  // Taille (px) d'une carte posée sur le board, mesurée sur mobile pour que les slots du
  // pool aient la MÊME dimension (parité avec le desktop). null = non mesuré (desktop).
  const [boardCardPx, setBoardCardPx] = useState<number | null>(null)

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
  const isRevealed = !!solution
  // Aussi le gate de gel du plateau : quand true (board deviné, plus de tentatives, ou cooldown de révélation), le drag/clic/rotation sont désactivés.
  const canMoveToNext = isBoardGuessed || (remainingAttempts === 0 && !isValidationPending) || isRevealed

  const hasTriedPlacement = useMemo(
    () =>
      (['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'] as const).some((pos) => {
        if (correctlyPlacedPositions.includes(pos)) return false
        const c = guessedPositions[pos]
        return !!c && isPlacementAlreadyTried(failedPlacements, c.cardId, pos, c.rotation)
      }),
    [guessedPositions, failedPlacements, correctlyPlacedPositions],
  )

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
  // 5 cartes au total (4 + leurre) → 5 slots : 3 à gauche, 2 à droite. Le 6e slot
  // (pool-5) n'est jamais peuplé → retiré (sinon une 3e case vide s'affiche à droite).
  const poolRight = useMemo(
    () => [poolMapping[3], poolMapping[4]],
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

  const revealedCards = useMemo(
    () => computeRevealedCards(solution, correctlyPlacedPositions),
    [solution, correctlyPlacedPositions],
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

  // Server gates these to null until the current board is resolved (success or attempts exhausted).
  // We pass them through verbatim — ClueInput shows the tooltip iff a value is non-null.
  const clueExplanations = useMemo(
    () => ({
      top: currentBoardClues.find((c) => c.direction === 'Top')?.explanation ?? null,
      right: currentBoardClues.find((c) => c.direction === 'Right')?.explanation ?? null,
      bottom: currentBoardClues.find((c) => c.direction === 'Bottom')?.explanation ?? null,
      left: currentBoardClues.find((c) => c.direction === 'Left')?.explanation ?? null,
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

  // Parité pool ↔ board : mesure le côté rendu du plateau → taille d'une carte
  // (cardSize/referenceSize) → dimensionne les slots du pool à l'identique des cartes
  // posées. ResizeObserver pour suivre rotation d'écran / resize. Re-déclenché quand le
  // board (re)monte après le chargement. Actif sur tous les appareils (laptop inclus).
  useEffect(() => {
    const el = boardRef.current
    if (!el) return
    const { cardSize, referenceSize } = CONSTANTS.ASSET_REFERENCES.board
    const update = () => {
      const w = el.getBoundingClientRect().width
      if (w > 0) setBoardCardPx(w * (cardSize / referenceSize))
    }
    update()
    const ro = new ResizeObserver(update)
    ro.observe(el)
    return () => ro.disconnect()
  }, [loading, currentBoardOwnerId])

  useEffect(() => {
    // Mobile (tactile) : pas de toast « C'est votre plateau » — il se superpose au board en
    // paysage (espace vertical compté) et fait doublon avec le contexte visuel. Desktop : conservé.
    if (isMyBoard && !isCoarse && currentBoardOwnerId && notifiedBoardId.current !== currentBoardOwnerId) {
      notifyTopCenter(i18n.t('common:notify.yourBoardToast'), { duration: 10000 })
      notifiedBoardId.current = currentBoardOwnerId
    } else if (!isMyBoard) {
      notifiedBoardId.current = null
    }
  }, [isMyBoard, isCoarse, currentBoardOwnerId, notifyTopCenter])

  // Auto-désélection : une carte sélectionnée (clic-clic) mais non déplacée est relâchée
  // après GUESSING_SELECTION_TIMEOUT_MS. Le timer est réarmé à chaque changement de sélection
  // et nettoyé au démontage → pas de swap surprise sur une sélection oubliée.
  useEffect(() => {
    if (!selectedSlotId) return
    const timer = setTimeout(
      () => setSelectedSlotId(null),
      CONSTANTS.GUESSING_SELECTION_TIMEOUT_MS,
    )
    return () => clearTimeout(timer)
  }, [selectedSlotId, setSelectedSlotId])

  // ─── Handlers ───────────────────────────────────────────────────────────────

  const handleRotateBoard = useCallback((direction: 'left' | 'right') => {
    if (isMyBoard || isValidationPending) return
    const rotationDelta = direction === 'right' ? 90 : -90
    const newRotation = safeCumulativeRotation + rotationDelta
    playSound('boardRotate')
    setCumulativeBoardRotation(newRotation)
    broadcastBoardRotation(newRotation)
  }, [isMyBoard, isValidationPending, safeCumulativeRotation, setCumulativeBoardRotation, broadcastBoardRotation])

  // ─── Clic-clic (tablette) ─────────────────────────────────────────────────
  // Le drag tactile étant peu fiable sur tablette, on offre un mode « clic carte
  // puis clic emplacement ». Réutilise handleDragEnd (logique 100 % slot-based).

  // Un slot peut être « source » (= porte une carte sélectionnable)
  const slotHasCard = useCallback(
    (slotId: string): boolean => {
      if (slotId.startsWith('pool-')) {
        const idx = parseInt(slotId.replace('pool-', ''), 10)
        return !!outsideCards[idx]
      }
      return !!guessedPositions[slotId]
    },
    [outsideCards, guessedPositions],
  )

  const handleSlotClick = useCallback(
    (slotId: string) => {
      if (isMyBoard || isValidationPending || canMoveToNext) return
      // Slots board déjà validés : verrouillés
      if (correctlyPlacedPositions.includes(slotId)) return

      // Re-clic sur le slot sélectionné → désélection
      if (selectedSlotId === slotId) {
        setSelectedSlotId(null)
        return
      }

      // Pas encore de sélection : on ne sélectionne qu'un slot porteur d'une carte
      if (!selectedSlotId) {
        if (slotHasCard(slotId)) setSelectedSlotId(slotId)
        return
      }

      // Une sélection existe → on traite ce clic comme la cible du déplacement
      setSelectedSlotId(null)
      void handleDragEnd(selectedSlotId, slotId)
    },
    [
      isMyBoard,
      isValidationPending,
      canMoveToNext,
      correctlyPlacedPositions,
      selectedSlotId,
      setSelectedSlotId,
      slotHasCard,
      handleDragEnd,
    ],
  )

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

  if (loading && !currentBoardOwnerId) {
    return (
      <div className="flex flex-col items-center justify-center min-h-svh gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500 text-lg">{t('preparingBoard')}</p>
      </div>
    )
  }

  // Only pass highlightedSlot for pool slot ids
  const poolHighlightedSlot =
    dragState.targetSlot?.startsWith('pool-') ? dragState.targetSlot : null

  return (
    <div className="guessing-fill-landscape flex flex-col h-[calc(100svh-2rem)]">
      {/* Incitation au paysage sur mobile portrait : le layout 3-colonnes ci-dessous
          ne tient pas en portrait étroit (cf. Axe 3). Auto-masqué hors portrait tactile. */}
      <LandscapePrompt />

      {/* min-h-0 autorise le flex-1 à se compresser ; container-type:size fournit 100cqh
          aux pools ET à la sous-zone plateau. overflow-hidden retiré : la politique
          plancher+scroll prend le relais (scroll seulement sous les planchers). */}
      <div
        className="flex flex-1 min-h-0 min-w-0 items-start justify-center px-2 py-0.5 gap-2 overflow-visible"
        style={{ containerType: 'size' }}
      >
        {/* Pool Gauche — self-stretch sur tactile : le wrapper prend toute la hauteur de la
            rangée pour que la pool (h-full + justify-end) aligne ses cartes par le bas, sur la
            même grille que la pool droite. */}
        <div className="flex-none self-stretch">
          <OutsideCardPool
            cards={poolLeft}
            startIndex={0}
            cardSizePx={boardCardPx}
            disabled={isMyBoard || canMoveToNext}
            displacedSlot={displacedSlot}
            highlightedSlot={poolHighlightedSlot}
            dragHandlers={isMyBoard || isValidationPending || canMoveToNext ? undefined : createDragHandlers}
            onSlotClick={isMyBoard || isValidationPending || canMoveToNext ? undefined : handleSlotClick}
            selectedSlotId={selectedSlotId}
            dragSourceCardId={dragState.draggedCardId}
            dragSourceSlot={dragState.sourceSlot}
          />
        </div>

        {/* Board Central */}
        <div data-testid="guessing-board" className="flex-1 flex flex-col items-center justify-center gap-1 min-w-0 min-h-0 max-h-full max-w-[1000px] self-stretch overflow-visible">
          {/* Sous-zone plateau : flex-1 prend la hauteur résiduelle de la colonne (= hauteur
              rangée − contrôles) ; container-type:size en fait le conteneur de référence du
              plateau → 100cqw = largeur colonne centrale, 100cqh = hauteur sous-zone. */}
          <div
            className="flex-1 min-h-0 w-full flex items-center justify-center"
            style={{ containerType: 'size' }}
          >
            <GuessingBoardSection
              boardRef={boardRef}
              boardCards={boardCards}
              boardGuessedCards={boardGuessedCards}
              clues={clues}
              clueExplanations={clueExplanations}
              rotation={safeCumulativeRotation}
              currentBoardOwnerId={currentBoardOwnerId}
              isMyBoard={isMyBoard}
              isValidationPending={isValidationPending}
              canMoveToNext={canMoveToNext}
              correctlyPlacedPositions={correctlyPlacedPositions}
              revealedCards={revealedCards}
              displacedSlot={displacedSlot}
              dragState={dragState}
              createDragHandlers={createDragHandlers}
              onSlotClick={handleSlotClick}
              selectedSlot={selectedSlotId}
            />
          </div>

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
            hasTriedPlacement={hasTriedPlacement}
          />
        </div>

        {/* Pool Droit — self-stretch sur tactile (cf. pool gauche). Le padding-top de
            `guessing-pool-right` (dégagement chip + CTA fixes) reste la borne haute ; avec
            l'alignement par le bas, ses cartes coïncident avec les rangées basses de la pool
            gauche → grille commune. */}
        <div className="guessing-pool-right flex-none self-stretch">
          <OutsideCardPool
            cards={poolRight}
            startIndex={3}
            cardSizePx={boardCardPx}
            disabled={isMyBoard || canMoveToNext}
            displacedSlot={displacedSlot}
            highlightedSlot={poolHighlightedSlot}
            dragHandlers={isMyBoard || isValidationPending || canMoveToNext ? undefined : createDragHandlers}
            onSlotClick={isMyBoard || isValidationPending || canMoveToNext ? undefined : handleSlotClick}
            selectedSlotId={selectedSlotId}
            dragSourceCardId={dragState.draggedCardId}
            dragSourceSlot={dragState.sourceSlot}
          />
        </div>
      </div>

      {/* Drag Overlay */}
      {dragState.isDragging && dragState.draggedCardId && (() => {
        const draggedCard = findDraggedCard()
        if (!draggedCard) return null

        const isFromOutside = outsideCards.some((c) => c?.cardId === draggedCard.cardId)
        // Taille de carte rendue = côté du plateau × (cardSize / referenceSize). Mesuré sur
        // boardRef (stable pendant un drag). Repli sur la constante si le plateau n'est pas monté.
        const { cardSize, referenceSize } = CONSTANTS.ASSET_REFERENCES.board
        const boardWidth = boardRef.current?.getBoundingClientRect().width ?? 0
        const overlaySize = boardWidth > 0
          ? boardWidth * (cardSize / referenceSize)
          : CONSTANTS.ASSET_REFERENCES.pool.dragOverlayPx
        return (
          <div
            className="fixed pointer-events-none z-[1000]"
            style={{
              left: dragState.dragPosition.x,
              top: dragState.dragPosition.y,
              transform: 'translate(-50%, -50%)',
              width: overlaySize,
              height: overlaySize,
            }}
          >
            <DraggableCard
              card={draggedCard}
              index={isFromOutside ? outsideCards.findIndex((c) => c?.cardId === draggedCard.cardId) : 0}
              isOutside={isFromOutside}
              disabled={true}
              isSelected={true}
              dragRotationOverride={isFromOutside ? 0 : -safeCumulativeRotation}
              isDragOverlay={true}
            />
          </div>
        )
      })()}
    </div>
  )
}
