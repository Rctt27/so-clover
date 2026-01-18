import { useDroppable } from '@dnd-kit/core'
import { DraggableCard } from './DraggableCard'
import { CardInfoResponse } from '../../types/game'
import { useGuessingStore } from '../../core/store'

export interface OutsideCardPoolProps {
  cards: (CardInfoResponse | null)[]
  startIndex: number
  disabled?: boolean
  swappingPositions?: { activePos: string, displacedPos: string } | null
}

export const OutsideCardPool = ({ 
  cards, 
  startIndex, 
  disabled = false,
  swappingPositions
}: OutsideCardPoolProps) => {
  const { selectedCardId, setSelectedCardId, isValidationPending } = useGuessingStore()

  return (
    <div className="flex flex-col gap-6 items-center p-4 min-w-[280px]">
      {cards.map((card, i) => {
        const slotIndex = startIndex + i;
        const slotId = `pool-${slotIndex}`;
        const isDisplaced = !!(swappingPositions && swappingPositions.displacedPos === slotId);
        
        return (
          <PoolSlot key={slotId} id={slotId} disabled={disabled || isValidationPending}>
            {card && (
              <DraggableCard
                key={card.cardId}
                card={card}
                index={slotIndex}
                isOutside={true}
                isLocked={disabled || isValidationPending}
                isSelected={selectedCardId === `outside-${card.cardId}`}
                isDisplaced={isDisplaced}
                onClick={() => !disabled && !isValidationPending && setSelectedCardId(selectedCardId === `outside-${card.cardId}` ? null : `outside-${card.cardId}`)}
              />
            )}
          </PoolSlot>
        );
      })}
    </div>
  )
}

interface PoolSlotProps {
  id: string;
  children: React.ReactNode;
  disabled?: boolean;
}

const PoolSlot = ({ id, children, disabled }: PoolSlotProps) => {
  const { isOver, setNodeRef } = useDroppable({
    id,
    disabled
  });

  return (
    <div 
      ref={setNodeRef}
      className={`relative w-[260px] h-[260px] rounded-2xl flex items-center justify-center transition-all duration-300 border-2 border-dashed ${
        isOver 
          ? 'bg-clover/15 border-clover border-solid shadow-[0_0_20px_rgba(76,175,80,0.3)] scale-105 z-10' 
          : 'border-gray-200 bg-gray-50/30'
      }`}
    >
      {/* Indicateur visuel au centre du slot vide */}
      {isOver && !children && (
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
          <div className="w-12 h-12 rounded-full bg-clover/20 animate-ping" />
        </div>
      )}
      {children}
    </div>
  );
}
