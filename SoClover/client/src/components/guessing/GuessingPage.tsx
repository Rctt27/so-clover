import { useState, useEffect, useRef, useMemo } from 'react'
import {
  DndContext,
  DragEndEvent,
  PointerSensor,
  KeyboardSensor,
  useSensor,
  useSensors,
  closestCenter,
  DragStartEvent,
  DragOverlay
} from '@dnd-kit/core'
import { LayoutGroup } from 'framer-motion'
import { useGameStore, useGuessingStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { useNotifications } from '../../hooks/useNotifications'
import { Board } from '../shared/Board'
import { OutsideCardPool } from './OutsideCardPool'
import { DraggableCard } from './DraggableCard'
import { CardData, rotationToDegrees, CardInfoResponse } from '../../types/game'
import { createRotationCompensationModifier } from '../../core/dragModifiers'
import { BoardRotationControls } from '../shared/BoardRotationControls'
import {
  RemoteCursorsLayer,
  useLocalCursorEmitter,
  useMouseTrackingEnabled
} from '../../features/mouseTracking'

export const GuessingPage = () => {
  const { playerId } = useGameStore()
  const { 
    currentBoardOwnerName, 
    currentBoardOwnerId,
    outsideCards, 
    guessedPositions, 
    currentBoardClues,
    cumulativeBoardRotation,
    setSelectedCardId,
    setCumulativeBoardRotation,
    isValidationPending,
    remainingAttempts,
    correctlyPlacedPositions
  } = useGuessingStore()
  const { 
    fetchGameState, 
    loading, 
    placeGuessingCard, 
    swapGuessingCards, 
    swapOutsidePoolCards,
    returnGuessingCard,
    validateBoard,
    broadcastBoardRotation,
    nextBoard
  } = useGameActions()
  const { notifyTopCenter } = useNotifications()
  const notifiedBoardId = useRef<string | null>(null)

  // Mouse tracking
  const boardRef = useRef<HTMLDivElement>(null)
  const isMouseTrackingEnabled = useMouseTrackingEnabled()
  
  const [activeCard, setActiveCard] = useState<CardInfoResponse | null>(null)
  const [activeData, setActiveData] = useState<any | null>(null)
  const [swappingCards, setSwappingCards] = useState<{ activePos: string, displacedPos: string } | null>(null)

  // Mapping local pour les slots du pool (0-5)
  // On initialise ou on met à jour le mapping quand outsideCards change
  const [poolMapping, setPoolMapping] = useState<Record<number, CardInfoResponse | null>>({
    0: null, 1: null, 2: null, 3: null, 4: null, 5: null
  });

  useEffect(() => {
    // Toujours synchroniser poolMapping avec outsideCards du store
    // Cela garantit la cohérence avec le backend après chaque mise à jour SignalR
    const mapping: Record<number, CardInfoResponse | null> = {
      0: null, 1: null, 2: null, 3: null, 4: null, 5: null
    };
    outsideCards.forEach((card, i) => {
      if (i < 6) mapping[i] = card;
    });
    setPoolMapping(mapping);
  }, [outsideCards]);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    }),
    useSensor(KeyboardSensor)
  )

  useEffect(() => {
    fetchGameState()
  }, [fetchGameState])

  const isMyBoard = currentBoardOwnerId === playerId

  // Activer l'émission locale seulement si pas owner du board
  useLocalCursorEmitter(boardRef, isMouseTrackingEnabled && !isMyBoard)

  // Valeur sécurisée de la rotation (fallback à 0 si undefined/NaN)
  const safeCumulativeRotation = typeof cumulativeBoardRotation === 'number' && !isNaN(cumulativeBoardRotation)
    ? cumulativeBoardRotation
    : 0

  // Déclencher la notification si c'est notre plateau et qu'on ne l'a pas encore fait pour ce plateau précis
  useEffect(() => {
    if (isMyBoard && currentBoardOwnerId && notifiedBoardId.current !== currentBoardOwnerId) {
      notifyTopCenter("C'est votre plateau ! Observez les autres joueurs.", { duration: 10000 })
      notifiedBoardId.current = currentBoardOwnerId
    } else if (!isMyBoard) {
      notifiedBoardId.current = null
    }
  }, [isMyBoard, currentBoardOwnerId, notifyTopCenter])

  const handleDragStart = (event: DragStartEvent) => {
    if (isMyBoard || isValidationPending || canMoveToNext) return;
    const { active } = event;
    const cardId = active.id as string;

    // Empêcher le scroll involontaire pendant le drag
    // Le navigateur peut scroller quand un élément focusé change de position
    document.documentElement.style.overflow = 'hidden';
    document.body.style.overflow = 'hidden';

    setSelectedCardId(cardId);
    setActiveCard(active.data.current?.card || null);
    setActiveData(active.data.current as any);
  }

  const handleDragMove = () => {
    if (isMyBoard || isValidationPending || canMoveToNext) return;
    // On pourrait ajouter une logique ici si nécessaire,
    // mais dnd-kit gère déjà la position de l'overlay.
  };

  const handleDragCancel = () => {
    // Restaurer le scroll en cas d'annulation du drag
    document.documentElement.style.overflow = '';
    document.body.style.overflow = '';

    setSelectedCardId(null);
    setActiveCard(null);
    setActiveData(null);
  };

  const handleDragEnd = async (event: DragEndEvent) => {
    // Restaurer le scroll
    document.documentElement.style.overflow = '';
    document.body.style.overflow = '';

    if (isMyBoard || isValidationPending) return;
    const { active, over } = event;
    setSelectedCardId(null);
    setActiveCard(null);
    setActiveData(null);

    const activeData = active.data.current;
    if (!activeData || !over) {
      return;
    }

    const overId = over.id as string;
    const activeId = active.id as string;

    // Ne rien faire si on drop sur soi-même
    if (activeId === overId) return;

    // Déterminer la source
    const isFromBoard = !activeData.isOutside;
    const sourceSlot = isFromBoard 
      ? Object.entries(guessedPositions).find(([_, card]) => card?.cardId === activeData.card.cardId)?.[0]
      : Object.entries(poolMapping).find(([_, card]) => card?.cardId === activeData.card.cardId)?.[0];

    if (!sourceSlot) {
      // Fallback si la recherche par ID échoue (ne devrait pas arriver si les IDs sont stables)
      return;
    }

    // Déterminer la cible
    const isOverBoard = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft'].includes(overId) || overId.startsWith('board-');
    const isOverPool = overId.startsWith('pool-') || overId.startsWith('outside-');

    const targetSlot = isOverBoard
      ? (overId.startsWith('board-') 
          ? Object.entries(guessedPositions).find(([_, card]) => card?.cardId === overId.replace('board-', ''))?.[0]
          : overId)
      : (isOverPool
          ? (overId.startsWith('outside-')
              ? Object.entries(poolMapping).find(([_, card]) => card?.cardId === overId.replace('outside-', ''))?.[0]
              : overId.replace('pool-', ''))
          : null);

    if (!targetSlot) return;

    // Cas 1: Board -> Board (Swap)
    if (isFromBoard && isOverBoard) {
      if (sourceSlot !== targetSlot) {
        setSwappingCards({ activePos: sourceSlot, displacedPos: targetSlot });
        await swapGuessingCards(sourceSlot, targetSlot);
        setTimeout(() => setSwappingCards(null), 500);
      }
    }
    // Cas 2: Pool -> Board
    else if (!isFromBoard && isOverBoard) {
      setSwappingCards({ activePos: `pool-${sourceSlot}`, displacedPos: targetSlot });

      // sourceSlot est l'index dans poolMapping qui est synchronisé avec outsideCards du store
      // On utilise cet index directement car poolMapping reflète l'état du backend
      const poolIndex = Number(sourceSlot);
      if (!isNaN(poolIndex) && poolIndex >= 0 && poolIndex < 6) {
        // Vérification de cohérence: s'assurer que la carte existe à cet index
        const cardAtIndex = outsideCards[poolIndex];
        if (cardAtIndex && cardAtIndex.cardId === activeData.card.cardId) {
          await placeGuessingCard(poolIndex, targetSlot);
        } else {
          // Désynchronisation détectée - rafraîchir l'état
          console.warn('[GuessingPage] Pool desync detected, refreshing state');
          await fetchGameState();
        }
      }
      setTimeout(() => setSwappingCards(null), 500);
    }
    // Cas 3: Board -> Pool
    else if (isFromBoard && isOverPool) {
      setSwappingCards({ 
        activePos: sourceSlot, 
        displacedPos: `pool-${targetSlot}`
      });
      await returnGuessingCard(sourceSlot);
      // On attend un peu que l'animation se termine avant de reset swappingCards
      setTimeout(() => setSwappingCards(null), 500);
    }
  // Cas 4: Pool -> Pool (Swap)
    else if (!isFromBoard && !isOverBoard && isOverPool) {
      const sIdx = Number(sourceSlot);
      const tIdx = Number(targetSlot);
      if (sIdx !== tIdx && !isNaN(sIdx) && !isNaN(tIdx)) {
        // Pas de mise à jour optimiste - on laisse SignalR synchroniser poolMapping via outsideCards
        // Cela évite les désynchronisations entre l'état local et le backend
        await swapOutsidePoolCards(sIdx, tIdx);
      }
    }
  }

  // Données pour les pools
  const poolLeft = [poolMapping[0], poolMapping[1], poolMapping[2]]
  const poolRight = [poolMapping[3], poolMapping[4], poolMapping[5]]

  // Conversion des indices pour le composant Board
  const clues = {
    top: currentBoardClues.find(c => c.direction === 'Top')?.text || '',
    right: currentBoardClues.find(c => c.direction === 'Right')?.text || '',
    bottom: currentBoardClues.find(c => c.direction === 'Bottom')?.text || '',
    left: currentBoardClues.find(c => c.direction === 'Left')?.text || '',
  }

  // Conversion des cartes placées pour le composant Board
  const boardCards: (CardData | null)[] = [
    guessedPositions['TopLeft'] ? { 
      words: [guessedPositions['TopLeft'].topWord, guessedPositions['TopLeft'].rightWord, guessedPositions['TopLeft'].bottomWord, guessedPositions['TopLeft'].leftWord],
      rotation: rotationToDegrees(guessedPositions['TopLeft'].rotation)
    } : null,
    guessedPositions['TopRight'] ? { 
      words: [guessedPositions['TopRight'].topWord, guessedPositions['TopRight'].rightWord, guessedPositions['TopRight'].bottomWord, guessedPositions['TopRight'].leftWord],
      rotation: rotationToDegrees(guessedPositions['TopRight'].rotation)
    } : null,
    guessedPositions['BottomRight'] ? { 
      words: [guessedPositions['BottomRight'].topWord, guessedPositions['BottomRight'].rightWord, guessedPositions['BottomRight'].bottomWord, guessedPositions['BottomRight'].leftWord],
      rotation: rotationToDegrees(guessedPositions['BottomRight'].rotation)
    } : null,
    guessedPositions['BottomLeft'] ? { 
      words: [guessedPositions['BottomLeft'].topWord, guessedPositions['BottomLeft'].rightWord, guessedPositions['BottomLeft'].bottomWord, guessedPositions['BottomLeft'].leftWord],
      rotation: rotationToDegrees(guessedPositions['BottomLeft'].rotation)
    } : null,
  ]

  const boardGuessedCards = [
    guessedPositions['TopLeft'],
    guessedPositions['TopRight'],
    guessedPositions['BottomRight'],
    guessedPositions['BottomLeft'],
  ]

  const isBoardFull = boardGuessedCards.every(c => c !== null)
  const isBoardGuessed = correctlyPlacedPositions.length === 4
  const canMoveToNext = isBoardGuessed || (remainingAttempts === 0 && !isValidationPending)

  // Modifier pour compenser le décalage curseur/carte quand le board est rotaté
  // Le modifier obtient isOutside directement depuis active.data.current de dnd-kit
  const dragModifiers = useMemo(() => {
    return [createRotationCompensationModifier(safeCumulativeRotation)]
  }, [safeCumulativeRotation])

  const handleRotateBoard = (direction: 'left' | 'right') => {
    if (isMyBoard || isValidationPending) return
    const rotationDelta = direction === 'right' ? 90 : -90
    const newRotation = safeCumulativeRotation + rotationDelta
    // Mark as local change to set timestamp and prevent race conditions with SignalR updates
    setCumulativeBoardRotation(newRotation, true)
    broadcastBoardRotation(newRotation)
  }

  if (loading && !currentBoardOwnerName) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500 text-lg">Préparation du plateau...</p>
      </div>
    )
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      modifiers={dragModifiers}
      autoScroll={false}
      onDragStart={handleDragStart}
      onDragMove={handleDragMove}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <div className="flex flex-col min-h-screen">
          {/* Header Info */}
          <div className="bg-white/30 backdrop-blur-sm shadow-sm p-4 text-center">
            <h1 className="text-2xl font-bold text-gray-800">
              Phase de Déduction
            </h1>
            <p className="text-gray-600">
              Plateau de <span className="font-bold text-clover-dark">{currentBoardOwnerName}</span>
            </p>
          </div>

          <LayoutGroup>
          <div className="flex flex-1 items-center justify-between px-8 py-4 gap-8 overflow-hidden">
            {/* Pool Gauche */}
            <div className="flex-none">
              <OutsideCardPool
                cards={poolLeft}
                startIndex={0}
                disabled={isMyBoard || canMoveToNext}
                swappingPositions={swappingCards}
              />
            </div>

            {/* Board Central */}
            <div className="flex-1 flex flex-col items-center justify-center gap-8 min-w-0">
              <div className="relative">
                <Board
                  ref={boardRef}
                  key={currentBoardOwnerId || 'no-board'}
                  cards={boardCards}
                  guessedCards={boardGuessedCards}
                  swappingPositions={swappingCards}
                  rotation={safeCumulativeRotation}
                  clues={clues}
                  animateEntry={true}
                  showClueInputs={false}
                  disabled={isMyBoard || isValidationPending || canMoveToNext}
                  isLocked={isMyBoard || isValidationPending || canMoveToNext}
                  correctPositions={correctlyPlacedPositions}
                  ownerId={currentBoardOwnerId || undefined}
                />
              </div>

              {/* Contrôles de rotation globale et validation */}
              <div className="flex flex-col items-center gap-6">
                {/* Espace fixe pour maintenir l'alignement vertical constant */}
                <div className="flex flex-col items-center gap-4" style={{ minHeight: '160px' }}>
                  {!isMyBoard && (
                    <>
                      <BoardRotationControls
                        rotation={safeCumulativeRotation}
                        onRotate={handleRotateBoard}
                        disabled={isValidationPending || isBoardGuessed}
                      />

                      <button
                        onClick={canMoveToNext ? nextBoard : validateBoard}
                        disabled={isValidationPending || (!isBoardFull && !canMoveToNext)}
                        className={`px-12 py-4 rounded-full text-white font-bold text-xl shadow-lg transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:scale-100 ${
                          canMoveToNext ? 'bg-blue-600 hover:bg-blue-700 shadow-blue/30' :
                          isBoardFull ? 'bg-clover hover:bg-clover-dark shadow-clover/30' : 'bg-gray-400'
                        }`}
                      >
                        {isValidationPending ? (
                          <div className="flex items-center gap-2">
                            <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                            Validation...
                          </div>
                        ) : canMoveToNext ? "Plateau suivant" : "Valider le plateau"}
                      </button>
                    </>
                  )}
                </div>
                
                <div className="text-center text-gray-700 bg-white/20 p-4 rounded-lg backdrop-blur-md border border-white/30 max-w-md">
                  <p className="text-sm italic">
                    {isBoardGuessed ? (
                      <span className="text-clover-dark font-bold text-lg">Bravo ! Plateau complété !</span>
                    ) : remainingAttempts === 0 && !isMyBoard ? (
                      <span className="text-red-600 font-bold">Dommage ! Plus de tentatives pour ce plateau.</span>
                    ) : (
                      <>
                        Replacer les 4 cartes sur le plateau en fonction des indices.
                        {!isMyBoard && " Attention à la carte bonus !"}
                        {isMyBoard && " Vous ne pouvez pas manipuler votre propre plateau."}
                      </>
                    )}
                  </p>
                  {!isBoardGuessed && remainingAttempts > 0 && !isMyBoard && (
                    <p className="text-clover-dark font-bold mt-2">
                      Tentatives restantes : {remainingAttempts}
                    </p>
                  )}
                </div>
              </div>
            </div>

            {/* Pool Droit */}
            <div className="flex-none">
              <OutsideCardPool
                cards={poolRight}
                startIndex={3}
                disabled={isMyBoard || canMoveToNext}
                swappingPositions={swappingCards}
              />
            </div>
          </div>
          </LayoutGroup>
        </div>

      <DragOverlay
        dropAnimation={null}
      >
        {activeCard && activeData ? (
          <div className="w-[180px] h-[180px] cursor-grabbing shadow-2xl scale-110 z-[1000]">
            <DraggableCard
              card={activeCard}
              index={activeData.index}
              isOutside={activeData.isOutside}
              disabled={true}
              isSelected={true}
              dragRotationOverride={activeData.isOutside ? 0 : -safeCumulativeRotation}
            />
          </div>
        ) : null}
      </DragOverlay>

      {/* Remote Cursors Layer - Global overlay */}
      <RemoteCursorsLayer
        boardRef={boardRef}
        enabled={isMouseTrackingEnabled}
      />
    </DndContext>
  )
}
