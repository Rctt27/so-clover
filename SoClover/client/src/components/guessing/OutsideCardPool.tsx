import React from 'react'
import { DraggableCard } from './DraggableCard'
import { CardInfoResponse } from '../../types/game'
import { useGuessingStore } from '../../core/store'

export interface OutsideCardPoolProps {
  cards: (CardInfoResponse | null)[]
  startIndex: number
  disabled?: boolean
  displacedSlot?: string | null
  /** Currently highlighted slot id (from dragState.targetSlot) */
  highlightedSlot?: string | null
  /** Drag handlers factory from useCardDrag */
  dragHandlers?: (slotId: string, cardId: string) => { onPointerDown: (e: React.PointerEvent) => void }
  /** Card id currently being dragged (to hide it in source slot) */
  dragSourceCardId?: string | null
  /** Source slot id of the current/last drag (paired with dragSourceCardId).
   *  Évite de masquer la carte au slot d'arrivée après que SignalR l'y a déplacée. */
  dragSourceSlot?: string | null
}

interface PoolSlotProps {
  id: string;
  children: React.ReactNode;
  isHighlighted: boolean;
}

const PoolSlot = ({ id, children, isHighlighted }: PoolSlotProps) => (
  <div
    data-slot-id={id}
    className={`relative w-[260px] h-[260px] rounded-2xl flex items-center justify-center transition-all duration-300 border-2 border-dashed ${
      isHighlighted
        ? 'bg-clover/15 border-clover border-solid shadow-[0_0_20px_rgba(76,175,80,0.3)] scale-105 z-10'
        : 'border-gray-200 bg-gray-50/30'
    }`}
  >
    {/* Indicateur visuel au centre du slot vide */}
    {isHighlighted && !children && (
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
        <div className="w-12 h-12 rounded-full bg-clover/20 animate-ping" />
      </div>
    )}
    {children}
  </div>
);

export const OutsideCardPool = ({
  cards,
  startIndex,
  disabled = false,
  displacedSlot,
  highlightedSlot,
  dragHandlers,
  dragSourceCardId,
  dragSourceSlot,
}: OutsideCardPoolProps) => {
  const { selectedCardId, setSelectedCardId, isValidationPending } = useGuessingStore()

  return (
    <div className="flex flex-col gap-6 items-center p-4 min-w-[280px]">
      {cards.map((card, i) => {
        const slotIndex = startIndex + i;
        const slotId = `pool-${slotIndex}`;
        const isDisplaced = !!(displacedSlot && displacedSlot === slotId);
        const isHighlighted = highlightedSlot === slotId;

        return (
          <PoolSlot key={slotId} id={slotId} isHighlighted={isHighlighted}>
            {card && (
              <DraggableCard
                key={card.cardId}
                card={card}
                index={slotIndex}
                isOutside={true}
                isLocked={disabled || isValidationPending}
                isSelected={selectedCardId === `outside-${card.cardId}`}
                isDisplaced={isDisplaced}
                isDragSource={dragSourceCardId === card.cardId && dragSourceSlot === slotId}
                onClick={() => !disabled && !isValidationPending && setSelectedCardId(selectedCardId === `outside-${card.cardId}` ? null : `outside-${card.cardId}`)}
                onPointerDown={
                  dragHandlers && !disabled && !isValidationPending
                    ? dragHandlers(slotId, card.cardId).onPointerDown
                    : undefined
                }
              />
            )}
          </PoolSlot>
        );
      })}
    </div>
  )
}
