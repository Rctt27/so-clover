import { describe, it, expect } from 'vitest'
import { draggableCardArePropsEqual } from './draggableCardArePropsEqual'
import type { DraggableCardProps } from './DraggableCard'
import type { CardInfoResponse } from '../../types/game'

const card = (id: string, rotation = 'None'): CardInfoResponse => ({
  cardId: id, topWord: 't', rightWord: 'r', bottomWord: 'b', leftWord: 'l',
  rotation,
})

const baseProps = (): DraggableCardProps => ({
  card: card('c1'),
  index: 0,
  isOutside: false,
  isLocked: false,
  isCorrect: false,
  isSelected: false,
  disabled: false,
  isDisplaced: false,
  isDragSource: false,
  isDragTarget: false,
})

describe('draggableCardArePropsEqual', () => {
  it('returns true for identical props', () => {
    expect(draggableCardArePropsEqual(baseProps(), baseProps())).toBe(true)
  })

  it('returns false when cardId changes', () => {
    const a = baseProps()
    const b = { ...baseProps(), card: card('c2') }
    expect(draggableCardArePropsEqual(a, b)).toBe(false)
  })

  it('returns false when card.rotation changes', () => {
    const a = baseProps()
    const b = { ...baseProps(), card: card('c1', 'Clockwise90') }
    expect(draggableCardArePropsEqual(a, b)).toBe(false)
  })

  it('returns false when isLocked toggles', () => {
    const a = baseProps()
    const b = { ...baseProps(), isLocked: true }
    expect(draggableCardArePropsEqual(a, b)).toBe(false)
  })

  it('returns false when isDragSource toggles', () => {
    const a = baseProps()
    const b = { ...baseProps(), isDragSource: true }
    expect(draggableCardArePropsEqual(a, b)).toBe(false)
  })

  it('returns false when isDragOverlay toggles', () => {
    const a = baseProps()
    const b = { ...baseProps(), isDragOverlay: true }
    expect(draggableCardArePropsEqual(a, b)).toBe(false)
  })

  it('ignores referential change of onPointerDown when value-equivalent props are unchanged', () => {
    const handlerA = () => {}
    const handlerB = () => {}
    const a = { ...baseProps(), onPointerDown: handlerA }
    const b = { ...baseProps(), onPointerDown: handlerB }
    expect(draggableCardArePropsEqual(a, b)).toBe(true)
  })
})
