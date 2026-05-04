import type { DraggableCardProps } from './DraggableCard'

export function draggableCardArePropsEqual(
  prev: DraggableCardProps,
  next: DraggableCardProps,
): boolean {
  if (prev.card.cardId !== next.card.cardId) return false
  if (prev.card.rotation !== next.card.rotation) return false
  if (prev.index !== next.index) return false
  if (prev.isOutside !== next.isOutside) return false
  if (prev.isLocked !== next.isLocked) return false
  if (prev.isCorrect !== next.isCorrect) return false
  if (prev.isSelected !== next.isSelected) return false
  if (prev.disabled !== next.disabled) return false
  if (prev.isDisplaced !== next.isDisplaced) return false
  if (prev.isDragSource !== next.isDragSource) return false
  if (prev.isDragTarget !== next.isDragTarget) return false
  if (prev.dragRotationOverride !== next.dragRotationOverride) return false
  return true
}
