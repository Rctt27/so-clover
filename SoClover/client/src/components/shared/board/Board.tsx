import React from 'react'
import { motion } from 'framer-motion'
import { CardAssembler } from '../card/CardAssembler'
import { ClueInput } from '../card/ClueInput'
import { CardData } from '../../../types/game'
import { DraggableCard } from '../../guessing/DraggableCard'
import { CONSTANTS } from '../../../core/constants'
import { LOGICAL_SLOTS } from '../../../core/utils'
import { CloverBoard } from './CloverBoard'

export interface BoardProps {
  cards: (CardData | null)[]; // 4 cards: [TopLeft, TopRight, BottomRight, BottomLeft]
  rotation?: number;
  clues?: {
    top: string;
    right: string;
    bottom: string;
    left: string;
  };
  /** Per-direction LLM explanations. Server-gated: each value is null until the
   *  current Guessing board is resolved. Only read by read-only clue displays. */
  clueExplanations?: {
    top: string | null;
    right: string | null;
    bottom: string | null;
    left: string | null;
  };
  guessedCards?: (import('../../../types/game').CardInfoResponse | null)[]; // For DraggableCards in Guessing
  displacedSlot?: string | null;
  showClueInputs?: boolean;
  onClueSave?: (position: 'top' | 'right' | 'bottom' | 'left', text: string) => Promise<void>;
  children?: React.ReactNode;
  className?: string;
  animateEntry?: boolean;
  disabled?: boolean;
  isLocked?: boolean;
  correctPositions?: string[];
  ownerId?: string;
  /** Currently highlighted drop target slot id (from dragState.targetSlot) */
  highlightedSlot?: string | null;
  /** Drag handlers factory from useCardDrag */
  dragHandlers?: (slotId: string, cardId: string) => { onPointerDown: (e: React.PointerEvent) => void };
  /** Card id currently being dragged (to hide it in source slot) */
  dragSourceCardId?: string | null;
  /** Source slot id of the current/last drag (paired with dragSourceCardId).
   *  Une carte n'est considérée "source" que si SON slot ET SON cardId matchent —
   *  cela évite de cacher la carte au slot d'arrivée après que SignalR l'y a déplacée. */
  dragSourceSlot?: string | null;
  /** Slot currently hovered during drag (to show target visual on card) */
  dragTargetSlot?: string | null;
}

export const Board = React.memo(React.forwardRef<HTMLDivElement, BoardProps>(({
  cards,
  rotation = 0,
  clues,
  clueExplanations,
  guessedCards,
  displacedSlot,
  showClueInputs = false,
  onClueSave,
  children,
  className = '',
  animateEntry = false,
  disabled = false,
  isLocked = false,
  correctPositions = [],
  highlightedSlot,
  dragHandlers,
  dragSourceCardId,
  dragSourceSlot,
  dragTargetSlot,
}, ref) => {
  // Dimensions de référence du canvas CloverBoard (centralisées dans CONSTANTS.ASSET_REFERENCES)
  const { referenceSize: REFERENCE_SIZE, cardSize: CARD_SIZE, cardGap: CARD_GAP } = CONSTANTS.ASSET_REFERENCES.board;

  const { board: boardAnim } = CONSTANTS.THEME_CONFIG.animations;

  // Centres des emplacements logiques par défaut (0°) sur le canvas de 1300px
  // Les centres sont décalés de cardGap/2 vers l'extérieur pour créer un espacement entre cartes.
  // Formule : boardCenter ± (cardSize/2 + cardGap/2)
  const boardCenter = REFERENCE_SIZE / 2; // 650
  const slotOffset = CARD_SIZE / 2 + CARD_GAP / 2; // 162
  const visualSlots = [
    { x: boardCenter - slotOffset, y: boardCenter - slotOffset }, // Visual Top-Left
    { x: boardCenter + slotOffset, y: boardCenter - slotOffset }, // Visual Top-Right
    { x: boardCenter + slotOffset, y: boardCenter + slotOffset }, // Visual Bottom-Right
    { x: boardCenter - slotOffset, y: boardCenter + slotOffset }, // Visual Bottom-Left
  ]

  // Fonction pour calculer le style d'un slot
  const getSlotStyle = (index: number) => {
    const slot = visualSlots[index]

    const leftPx = slot.x - (CARD_SIZE / 2)
    const topPx = slot.y - (CARD_SIZE / 2)

    return {
      position: 'absolute' as const,
      left: `${(leftPx / REFERENCE_SIZE) * 100}%`,
      top: `${(topPx / REFERENCE_SIZE) * 100}%`,
      width: `${(CARD_SIZE / REFERENCE_SIZE) * 100}%`,
      height: `${(CARD_SIZE / REFERENCE_SIZE) * 100}%`,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
    }
  }

  // Calcul du mapping entre position visuelle et slot logique selon la rotation
  // Rotation horaire (90, 180, 270)
  const safeRotation = typeof rotation === 'number' && !isNaN(rotation) ? rotation : 0;
  const getLogicalIndexFromVisual = (vIndex: number) => {
    const rotationIndex = Math.round(((safeRotation % 360 + 360) % 360) / 90) % 4;
    return (vIndex - rotationIndex + 4) % 4;
  }


  const handleSaveClue = async (position: 'top' | 'right' | 'bottom' | 'left', text: string) => {
    if (onClueSave) {
      await onClueSave(position, text);
    }
  }

  const renderClues = (isInput: boolean) => {
    if (!clues && !isInput) return null;

    return (['top', 'right', 'bottom', 'left'] as const).map((pos) => {
      const clueValue = clues?.[pos] || '';

      if (isInput) {
        return (
          <div key={pos} className="pointer-events-auto">
            <ClueInput
              position={pos}
              value={clueValue}
              onSave={(text) => handleSaveClue(pos, text)}
              disabled={disabled}
            />
          </div>
        );
      }

      if (clueValue) {
        return (
          <div key={pos} className="pointer-events-auto">
            <ClueInput
              position={pos}
              value={clueValue}
              onSave={async () => {}}
              disabled={true}
              explanation={clueExplanations?.[pos] ?? null}
            />
          </div>
        );
      }

      return null;
    });
  };

  return (
    <div className="flex justify-center w-full">
      <div
        ref={ref}
        className={`relative w-full aspect-square ${className}`}
        style={{
          width: '100%',
          maxWidth: '1000px',
          minWidth: 'min(800px, 100vw - 2rem)',
        }}
      >
        {/* Layer 1: Drop target hit zones - zIndex: 10 */}
        <div className="absolute inset-0" style={{ zIndex: 10 }}>
          {[0, 1, 2, 3].map((vIndex) => {
            const lIndex = getLogicalIndexFromVisual(vIndex);
            const logicalPosName = LOGICAL_SLOTS[lIndex];
            return (
              <div
                key={vIndex}
                data-slot-id={logicalPosName}
                style={getSlotStyle(vIndex)}
                className={`rounded-lg transition-all duration-200 ${
                  highlightedSlot === logicalPosName
                    ? 'bg-clover/30 ring-8 ring-clover/50 scale-105 z-50 shadow-[0_0_20px_rgba(76,175,80,0.4)]'
                    : ''
                }`}
              >
                <div className="w-full h-full" />
              </div>
            );
          })}
        </div>

        {/* Layer 2: Rotating Content (Board + Cards + Clues) - zIndex: 20 */}
        <motion.div
          className="absolute inset-0"
          initial={animateEntry ? boardAnim.initial : false}
          animate={{
            ...boardAnim.animate,
            rotate: safeRotation
          }}
          style={{
            zIndex: 20,
            pointerEvents: 'none'
          }}
          transition={{
            rotate: { duration: 0.5, ease: 'easeInOut' as const },
            default: animateEntry ? boardAnim.transition : { duration: 0.5 }
          }}
        >
          {/* Board Canvas */}
          <CloverBoard />

          {/* Cards */}
          {LOGICAL_SLOTS.map((logicalPosName, lIndex) => {
            const cardData = cards[lIndex];
            const guessedCard = guessedCards?.[lIndex];

            if (!cardData && !guessedCard) return null;

            const isSlotLocked = isLocked || correctPositions.includes(logicalPosName);
            const isDisplaced = !!(displacedSlot && displacedSlot === logicalPosName);

            // On positionne la carte au slot VISUEL par défaut (index logique)
            // car le conteneur motion.div applique déjà la rotation globale du plateau.
            const vIndex = lIndex;

            return (
              <div key={logicalPosName} style={getSlotStyle(vIndex)}>
                <div className="w-full h-full pointer-events-auto">
                  {guessedCard ? (
                    <DraggableCard
                      key={guessedCard.cardId}
                      card={guessedCard}
                      index={lIndex}
                      isOutside={false}
                      disabled={disabled}
                      isLocked={isSlotLocked}
                      isCorrect={correctPositions.includes(logicalPosName)}
                      isDisplaced={isDisplaced}
                      isDragSource={dragSourceCardId === guessedCard.cardId && dragSourceSlot === logicalPosName}
                      isDragTarget={dragTargetSlot === logicalPosName}
                      onPointerDown={
                        dragHandlers
                          ? dragHandlers(logicalPosName, guessedCard.cardId).onPointerDown
                          : undefined
                      }
                    />
                  ) : cardData ? (
                    <CardAssembler
                      words={cardData.words}
                      rotation={cardData.rotation}
                      animateEntry={animateEntry}
                    />
                  ) : null}
                </div>
              </div>
            );
          })}

          {/* Clues (Read-only) */}
          {!showClueInputs && (
            <div className="absolute inset-0 pointer-events-none">
              <div className="relative w-full h-full pointer-events-none">
                {renderClues(false)}
              </div>
            </div>
          )}

          {/* Clues (Input mode) */}
          {showClueInputs && (
            <div className="absolute inset-0 pointer-events-none">
              <div className="relative w-full h-full pointer-events-none">
                {renderClues(true)}
              </div>
            </div>
          )}
        </motion.div>

        {/* Additional content (cursors, etc.) */}
        <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 100 }}>
          {children}
        </div>
      </div>
    </div>
  )
}));

Board.displayName = 'Board';
