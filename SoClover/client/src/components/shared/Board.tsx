import { motion } from 'framer-motion'
import { useDroppable } from '@dnd-kit/core'
import { Card } from './Card'
import { ClueInput } from './ClueInput'
import { CardData } from '../../types/game'
import { DraggableCard } from '../guessing/DraggableCard'
import { CONSTANTS } from '../../core/constants'
import { degreesToRotationIndex, LOGICAL_SLOTS } from '../../core/utils'
import boardImage from '../../assets/images/Board.png'

export interface BoardProps {
  cards: (CardData | null)[]; // 4 cards: [TopLeft, TopRight, BottomRight, BottomLeft]
  rotation?: number;
  clues?: {
    top: string;
    right: string;
    bottom: string;
    left: string;
  };
  guessedCards?: (import('../../types/game').CardInfoResponse | null)[]; // For DraggableCards in Guessing
  swappingPositions?: { activePos: string, displacedPos: string } | null;
  showClueInputs?: boolean;
  onClueSave?: (position: 'top' | 'right' | 'bottom' | 'left', text: string) => Promise<void>;
  children?: React.ReactNode;
  className?: string;
  animateEntry?: boolean;
  disabled?: boolean;
  isLocked?: boolean;
  correctPositions?: string[];
}

export const Board = ({ 
  cards, 
  rotation = 0, 
  clues, 
  guessedCards,
  swappingPositions,
  showClueInputs = false, 
  onClueSave,
  children, 
  className = '', 
  animateEntry = false,
  disabled = false,
  isLocked = false,
  correctPositions = []
}: BoardProps) => {
  // Dimensions de référence de Board.png
  const REFERENCE_SIZE = 1190
  const CARD_SIZE = 320 // Taille de Card_320px.png

  const { board: boardAnim } = CONSTANTS.THEME_CONFIG.animations;
  const rotationTransition = { duration: 0.5, ease: 'easeInOut' };

  // Centres des emplacements VISUELS (coordonnées x, y sur 1190px)
  // Ces positions sont fixes sur l'écran.
  const visualSlots = [
    { x: 426, y: 433 }, // Visual Top-Left (Index 0)
    { x: 753, y: 433 }, // Visual Top-Right (Index 1)
    { x: 753, y: 760 }, // Visual Bottom-Right (Index 2)
    { x: 426, y: 760 }, // Visual Bottom-Left (Index 3)
  ]

  const rotIdx = degreesToRotationIndex(rotation);

  // Fonction pour calculer le style d'un slot en fonction de son index VISUEL
  const getVisualSlotStyle = (visualIndex: number) => {
    const slot = visualSlots[visualIndex]
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
      zIndex: 20
    }
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
        className={`relative w-full aspect-square ${className}`}
        style={{
          width: '100%',
          maxWidth: '800px',
          minWidth: 'min(800px, 100vw - 2rem)',
        }}
      >
        <motion.div
          className="absolute inset-0"
          initial={animateEntry ? boardAnim.initial : false}
          animate={{ 
            ...boardAnim.animate,
            rotate: rotation 
          }}
          transition={{
            rotate: rotationTransition,
            default: animateEntry ? boardAnim.transition : { duration: 0.5 }
          }}
        >
          <img
            src={boardImage}
            alt="Board"
            className="absolute inset-0 w-full h-full object-contain pointer-events-none"
            style={{ zIndex: 0 }}
            draggable={false}
          />

          {/* Clues display - In Guessing phase (read-only), we put them inside the same motion.div for perfect sync */}
          {!showClueInputs && (
            <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 5 }}>
              <div className="relative w-full h-full pointer-events-none">
                {renderClues(false)}
              </div>
            </div>
          )}
        </motion.div>

        {/* Cards container - Les slots dnd-kit sont fixes ici */}
        <div className="absolute inset-0" style={{ zIndex: 10 }}>
          {[0, 1, 2, 3].map((vIndex) => {
            // On trouve quelle position LOGIQUE correspond à ce slot VISUEL
            const lIndex = (vIndex + rotIdx) % 4;
            const logicalPosName = LOGICAL_SLOTS[lIndex];
            
            const isDisplaced = !!(swappingPositions && swappingPositions.displacedPos === logicalPosName);
            const isSlotLocked = isLocked || correctPositions.includes(logicalPosName);
            
            return (
              <DroppableSlot 
                key={logicalPosName} 
                id={logicalPosName} 
                style={getVisualSlotStyle(vIndex)}
                disabled={disabled || isSlotLocked}
              >
                {guessedCards?.[lIndex] ? (
                  <DraggableCard 
                    card={guessedCards[lIndex]!}
                    index={lIndex}
                    isOutside={false}
                    disabled={disabled}
                    isLocked={isSlotLocked}
                    isCorrect={correctPositions.includes(logicalPosName)}
                    isDisplaced={isDisplaced}
                  />
                ) : cards[lIndex] ? (
                  <motion.div
                    key={`static-${lIndex}`}
                    animate={{ rotate: rotation }}
                    transition={rotationTransition}
                    style={{ 
                      width: '100%', 
                      height: '100%',
                    }}
                  >
                    <Card 
                      words={cards[lIndex]!.words} 
                      rotation={cards[lIndex]!.rotation} 
                      animateEntry={animateEntry} 
                    />
                  </motion.div>
                ) : null}
              </DroppableSlot>
            );
          })}
        </div>

        {/* Clue Layer (Input mode) - We keep it outside for z-index 30, but with same transition */}
        {showClueInputs && (
          <motion.div 
            className="absolute inset-0 pointer-events-none" 
            style={{ zIndex: 30 }}
            animate={{ rotate: rotation }}
            transition={rotationTransition}
          >
            <div className="relative w-full h-full pointer-events-none">
              {renderClues(true)}
            </div>
          </motion.div>
        )}

        {/* Additional content (clues, cursors, etc.) */}
        {children}
      </div>
    </div>
  )
}

interface DroppableSlotProps {
  id: string;
  style: React.CSSProperties;
  children: React.ReactNode;
  disabled?: boolean;
}

const DroppableSlot = ({ id, style, children, disabled }: DroppableSlotProps) => {
  const { isOver, setNodeRef } = useDroppable({
    id,
    disabled
  });

  return (
    <div 
      ref={setNodeRef} 
      style={style}
      className={`rounded-lg transition-colors duration-200 ${isOver ? 'bg-clover/20 ring-4 ring-clover/50 scale-105 z-50' : ''}`}
    >
      {children}
    </div>
  );
}
