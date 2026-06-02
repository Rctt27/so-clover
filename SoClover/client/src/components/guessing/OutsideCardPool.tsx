import React from 'react'
import { DraggableCard } from './DraggableCard'
import { CardInfoResponse } from '../../types/game'
import { useGuessingStore } from '../../core/store'
import { CONSTANTS } from '../../core/constants'

export interface OutsideCardPoolProps {
  cards: (CardInfoResponse | null)[]
  startIndex: number
  disabled?: boolean
  displacedSlot?: string | null
  /** Currently highlighted slot id (from dragState.targetSlot) */
  highlightedSlot?: string | null
  /** Drag handlers factory from useCardDrag */
  dragHandlers?: (slotId: string, cardId: string) => { onPointerDown: (e: React.PointerEvent) => void }
  /** Clic-clic (tablette) : sélection/placement par slot. undefined si désactivé. */
  onSlotClick?: (slotId: string) => void
  /** Slot actuellement sélectionné (clic-clic) — pour l'anneau visuel. */
  selectedSlotId?: string | null
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
  onClick?: () => void;
}

const PoolSlot = ({ id, children, isHighlighted, onClick }: PoolSlotProps) => {
  const { slotMinPx, slotMaxPx } = CONSTANTS.ASSET_REFERENCES.pool;
  // Slot carré dimensionné sur la hauteur de la rangée centrale (100cqh fourni par le
  // container-type:size de GuessingPage). Réserve 4rem = 2×gap-6 (2×1.5rem) + pb-4 (1rem)
  // pour les 3 slots empilés, puis divise par 3. Borné [min, max].
  const sideLength = `clamp(${slotMinPx}px, calc((100cqh - 4rem) / 3), ${slotMaxPx}px)`;
  return (
    <div
      data-slot-id={id}
      onClick={onClick}
      style={{ width: sideLength, height: sideLength }}
      className={`relative rounded-2xl flex items-center justify-center transition-all duration-300 border-2 border-dashed ${
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
};

export const OutsideCardPool = ({
  cards,
  startIndex,
  disabled = false,
  displacedSlot,
  highlightedSlot,
  dragHandlers,
  onSlotClick,
  selectedSlotId,
  dragSourceCardId,
  dragSourceSlot,
}: OutsideCardPoolProps) => {
  const { isValidationPending } = useGuessingStore()

  return (
    <div className="flex flex-col gap-6 items-center px-4 pb-4 pt-0">
      {cards.map((card, i) => {
        const slotIndex = startIndex + i;
        const slotId = `pool-${slotIndex}`;
        const isDisplaced = !!(displacedSlot && displacedSlot === slotId);
        const isHighlighted = highlightedSlot === slotId;
        const canInteract = !disabled && !isValidationPending;

        return (
          <PoolSlot
            key={slotId}
            id={slotId}
            isHighlighted={isHighlighted}
            onClick={canInteract && onSlotClick ? () => onSlotClick(slotId) : undefined}
          >
            {card && (
              <DraggableCard
                key={card.cardId}
                card={card}
                index={slotIndex}
                isOutside={true}
                isLocked={disabled || isValidationPending}
                isSelected={selectedSlotId === slotId}
                isDisplaced={isDisplaced}
                isDragSource={dragSourceCardId === card.cardId && dragSourceSlot === slotId}
                onPointerDown={
                  dragHandlers && canInteract
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
