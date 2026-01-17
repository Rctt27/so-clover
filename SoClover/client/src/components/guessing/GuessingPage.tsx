import { useState, useEffect, useRef } from 'react'
import { 
  DndContext, 
  DragEndEvent, 
  PointerSensor, 
  KeyboardSensor, 
  useSensor, 
  useSensors,
  closestCenter,
  DragStartEvent,
  DragOverlay,
  defaultDropAnimationSideEffects
} from '@dnd-kit/core'
import { LayoutGroup } from 'framer-motion'
import { useGameStore, useGuessingStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { useNotifications } from '../../hooks/useNotifications'
import { Board } from '../shared/Board'
import { OutsideCardPool } from './OutsideCardPool'
import { DraggableCard } from './DraggableCard'
import { CardData, rotationToDegrees, CardInfoResponse } from '../../types/game'

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
  
  const [activeCard, setActiveCard] = useState<CardInfoResponse | null>(null)
  const [activeData, setActiveData] = useState<any | null>(null)
  const [swappingCards, setSwappingCards] = useState<{ activePos: string, displacedPos: string } | null>(null)

  // Mapping local pour les slots du pool (0-5)
  // On initialise ou on met à jour le mapping quand outsideCards change
  const [poolMapping, setPoolMapping] = useState<Record<number, CardInfoResponse | null>>({
    0: null, 1: null, 2: null, 3: null, 4: null, 5: null
  });

  useEffect(() => {
    // On n'écrase le mapping local que si on n'est pas en train de draguer ou si les cartes ont réellement changé d'identités
    // ou si c'est la première fois qu'on reçoit les cartes.
    const currentCardIds = Object.values(poolMapping).filter(Boolean).map(c => c!.cardId).sort().join(',');
    const nextCardIds = outsideCards.filter(Boolean).map(c => c!.cardId).sort().join(',');

    if (currentCardIds !== nextCardIds || Object.values(poolMapping).every(v => v === null)) {
      const mapping: Record<number, CardInfoResponse | null> = {};
      outsideCards.forEach((card, i) => {
        mapping[i] = card;
      });
      setPoolMapping(mapping);
    }
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
    
    // Si c'est une carte du board, on veut désactiver temporairement les transitions de rotation
    // pour éviter les sauts visuels lors du drag (vu qu'on utilise un overlay)
    
    setSelectedCardId(cardId);
    setActiveCard(active.data.current?.card || null);
    setActiveData(active.data.current as any);
  }

  const handleDragEnd = async (event: DragEndEvent) => {
    if (isMyBoard || isValidationPending) return;
    const { active, over } = event;
    setSelectedCardId(null);
    setActiveCard(null);
    setActiveData(null);

    const activeData = active.data.current;
    if (!activeData || !over) {
      setActiveCard(null);
      setActiveData(null);
      setSelectedCardId(null);
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

    if (!sourceSlot) return;

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
        setSwappingCards(null);
      }
    }
    // Cas 2: Pool -> Board
    else if (!isFromBoard && isOverBoard) {
      const existingCard = guessedPositions[targetSlot];
      if (existingCard) {
        setSwappingCards({ activePos: `pool-${sourceSlot}`, displacedPos: targetSlot });
      }
      // Le backend attend l'index dans la liste outsideCards réelle, pas le slot index
      const realIndex = outsideCards.findIndex(c => c && c.cardId === activeData.card.cardId);
      if (realIndex !== -1) {
        await placeGuessingCard(realIndex, targetSlot);
      }
      setSwappingCards(null);
    }
    // Cas 3: Board -> Pool
    else if (isFromBoard && isOverPool) {
      const existingCard = poolMapping[Number(targetSlot)];
      if (existingCard) {
        setSwappingCards({ activePos: sourceSlot, displacedPos: `pool-${targetSlot}` });
      }
      await returnGuessingCard(sourceSlot);
      setSwappingCards(null);
    }
  // Cas 4: Pool -> Pool (Local UI Swap + Sync)
    else if (!isFromBoard && !isOverBoard && isOverPool) {
      const sIdx = Number(sourceSlot);
      const tIdx = Number(targetSlot);
      if (sIdx !== tIdx) {
        // Find cards before swap for optimistic mapping
        const sourceCard = poolMapping[sIdx];
        const targetCard = poolMapping[tIdx];

        if (!sourceCard) return;

        // Optimistic UI update
        const newMapping = { ...poolMapping };
        newMapping[sIdx] = targetCard;
        newMapping[tIdx] = sourceCard;
        setPoolMapping(newMapping);
        
        // Sync with backend using the real indices in outsideCards
        // On cherche les indices réels dans la liste immuable du store
        const activeRealIndex = outsideCards.findIndex(c => c && c.cardId === sourceCard.cardId);
        
        // Pour la cible, si c'est vide, on essaie de déduire l'index réel
        let targetRealIndex = -1;
        if (targetCard) {
          targetRealIndex = outsideCards.findIndex(c => c && c.cardId === targetCard.cardId);
        } else {
          // Si le slot est vide, on cherche un index null dans outsideCards qui correspondrait au slot
          // En fait, poolMapping est censé être aligné sur outsideCards au début.
          targetRealIndex = tIdx; 
        }

        if (activeRealIndex !== -1 && targetRealIndex !== -1 && activeRealIndex !== targetRealIndex) {
          await swapOutsidePoolCards(activeRealIndex, targetRealIndex);
        }
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

  const handleRotateBoard = (direction: 'Left' | 'Right') => {
    if (isMyBoard || isValidationPending) return
    const rotationDelta = direction === 'Right' ? 90 : -90
    const newRotation = cumulativeBoardRotation + rotationDelta
    setCumulativeBoardRotation(newRotation)
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
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
    >
      <LayoutGroup id="guessing-layout">
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
                  cards={boardCards}
                  guessedCards={boardGuessedCards}
                  swappingPositions={swappingCards}
                  rotation={cumulativeBoardRotation}
                  clues={clues}
                  animateEntry={true}
                  showClueInputs={false}
                  disabled={isMyBoard || isValidationPending || canMoveToNext}
                  isLocked={isMyBoard || isValidationPending || canMoveToNext}
                  correctPositions={correctlyPlacedPositions}
                />
              </div>

              {/* Contrôles de rotation globale et validation */}
              <div className="flex flex-col items-center gap-6">
                {!isMyBoard && (
                  <div className="flex items-center gap-8">
                    <button
                      onClick={() => handleRotateBoard('Left')}
                      disabled={isValidationPending || isBoardGuessed}
                      className="bg-white/80 hover:bg-white text-clover p-4 rounded-full shadow-xl transition-all hover:scale-110 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                      title="Faire pivoter le plateau vers la gauche"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path></svg>
                    </button>

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

                    <button
                      onClick={() => handleRotateBoard('Right')}
                      disabled={isValidationPending || isBoardGuessed}
                      className="bg-white/80 hover:bg-white text-clover p-4 rounded-full shadow-xl transition-all hover:scale-110 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                      title="Faire pivoter le plateau vers la droite"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M21 12a9 9 0 1 1-9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"></path><path d="M21 3v5h-5"></path></svg>
                    </button>
                  </div>
                )}
                
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
        </div>
      </LayoutGroup>

      <DragOverlay
        dropAnimation={{
          duration: 300,
          easing: 'cubic-bezier(0.18, 0.67, 0.6, 1.22)',
          sideEffects: defaultDropAnimationSideEffects({
            styles: {
              active: {
                opacity: '0',
              },
            },
          }),
        }}
      >
        {activeCard && activeData ? (
          <div className="w-[180px] h-[180px] cursor-grabbing shadow-2xl scale-110 z-[1000]">
            <DraggableCard 
              card={activeCard} 
              index={activeData.index} 
              isOutside={activeData.isOutside}
              disabled={true}
              isSelected={true}
            />
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  )
}
