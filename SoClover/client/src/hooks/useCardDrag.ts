import { useRef, useState, useCallback, useEffect } from 'react'
import { LOGICAL_SLOTS } from '../core/utils'

// ─── Public types ────────────────────────────────────────────────────────────

export interface DragState {
  isDragging: boolean
  draggedCardId: string | null
  sourceSlot: string | null
  targetSlot: string | null          // Currently hovered slot
  dragPosition: { x: number; y: number }  // Screen position for overlay
}

export interface UseCardDragOptions {
  boardRef: React.RefObject<HTMLElement>
  boardRotationDeg: number
  onDragEnd: (source: string, target: string) => void
  disabled?: boolean
}

// ─── Internal helpers ────────────────────────────────────────────────────────

const ACTIVATION_DISTANCE = 5  // px — minimum movement before drag starts

/**
 * Convert screen coordinates to board-local coordinates,
 * compensating for the board's CSS rotation.
 */
function screenToBoard(
  screenX: number,
  screenY: number,
  boardRect: DOMRect,
  rotationDeg: number
): { x: number; y: number } {
  // Translate to board center
  const cx = screenX - (boardRect.left + boardRect.width / 2)
  const cy = screenY - (boardRect.top + boardRect.height / 2)

  // Apply inverse rotation
  const rad = (-rotationDeg * Math.PI) / 180
  const bx = cx * Math.cos(rad) - cy * Math.sin(rad)
  const by = cx * Math.sin(rad) + cy * Math.cos(rad)

  // Translate back from board center
  return {
    x: bx + boardRect.width / 2,
    y: by + boardRect.height / 2,
  }
}

/**
 * Return the slot id of the closest drop target at (pointerX, pointerY),
 * or null if the pointer is not within any target's bounding rect (with tolerance).
 */
function findTargetAtPoint(
  pointerX: number,
  pointerY: number,
  boardRef: React.RefObject<HTMLElement>,
  boardRotationDeg: number
): string | null {
  const TOLERANCE = 16  // px — extra hit area around each slot

  // Gather all registered slots from the DOM
  const slotEls = Array.from(
    document.querySelectorAll<HTMLElement>('[data-slot-id]')
  )

  let bestId: string | null = null
  let bestDist = Infinity

  for (const el of slotEls) {
    const slotId = el.dataset.slotId!
    const isBoard = (LOGICAL_SLOTS as readonly string[]).includes(slotId)
    const rect = el.getBoundingClientRect()

    if (isBoard && boardRef.current) {
      // Convert pointer to board-local space
      const boardRect = boardRef.current.getBoundingClientRect()
      const local = screenToBoard(pointerX, pointerY, boardRect, boardRotationDeg)

      // The slot's bounding rect is in screen space because the board element is
      // rotated in CSS. We need the slot center in board-local space.
      // We use the screen-space rect center and apply the same inverse rotation.
      const slotCenterScreenX = rect.left + rect.width / 2
      const slotCenterScreenY = rect.top + rect.height / 2
      const slotLocal = screenToBoard(
        slotCenterScreenX,
        slotCenterScreenY,
        boardRect,
        boardRotationDeg
      )

      // Use board-local half-dimensions for the hit rect
      // (slot is a square card — use its unrotated size)
      const halfW = rect.width / 2 + TOLERANCE
      const halfH = rect.height / 2 + TOLERANCE

      if (
        local.x >= slotLocal.x - halfW &&
        local.x <= slotLocal.x + halfW &&
        local.y >= slotLocal.y - halfH &&
        local.y <= slotLocal.y + halfH
      ) {
        const dist = Math.hypot(local.x - slotLocal.x, local.y - slotLocal.y)
        if (dist < bestDist) {
          bestDist = dist
          bestId = slotId
        }
      }
    } else {
      // Pool slots — simple screen-space hit test
      const centerX = rect.left + rect.width / 2
      const centerY = rect.top + rect.height / 2
      const testX = pointerX
      const testY = pointerY

      if (
        testX >= centerX - rect.width / 2 - TOLERANCE &&
        testX <= centerX + rect.width / 2 + TOLERANCE &&
        testY >= centerY - rect.height / 2 - TOLERANCE &&
        testY <= centerY + rect.height / 2 + TOLERANCE
      ) {
        const dist = Math.hypot(testX - centerX, testY - centerY)
        if (dist < bestDist) {
          bestDist = dist
          bestId = slotId
        }
      }
    }
  }

  return bestId
}

// ─── Hook ────────────────────────────────────────────────────────────────────

const INITIAL_DRAG_STATE: DragState = {
  isDragging: false,
  draggedCardId: null,
  sourceSlot: null,
  targetSlot: null,
  dragPosition: { x: 0, y: 0 },
}

export function useCardDrag(options: UseCardDragOptions): {
  dragState: DragState
  createDragHandlers: (
    slotId: string,
    cardId: string
  ) => {
    onPointerDown: (e: React.PointerEvent) => void
  }
} {
  const { boardRef, boardRotationDeg, onDragEnd, disabled } = options

  const [dragState, setDragState] = useState<DragState>(INITIAL_DRAG_STATE)

  // Refs to hold live drag state without triggering re-renders
  const draggingRef = useRef(false)
  const activatedRef = useRef(false)  // true once 5 px threshold crossed
  const sourceSlotRef = useRef<string | null>(null)
  const cardIdRef = useRef<string | null>(null)
  const pointerIdRef = useRef<number | null>(null)
  const startPosRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 })
  const boardRotationRef = useRef(boardRotationDeg)
  const onDragEndRef = useRef(onDragEnd)
  const listenersRef = useRef<{ move: (e: PointerEvent) => void; up: (e: PointerEvent) => void } | null>(null)

  // Keep refs in sync with latest props without needing them in useCallback deps
  useEffect(() => {
    boardRotationRef.current = boardRotationDeg
  }, [boardRotationDeg])

  useEffect(() => {
    onDragEndRef.current = onDragEnd
  }, [onDragEnd])

  // Clean up when the component using this hook unmounts mid-drag
  useEffect(() => {
    return () => {
      if (listenersRef.current) {
        window.removeEventListener('pointermove', listenersRef.current.move)
        window.removeEventListener('pointerup', listenersRef.current.up)
        window.removeEventListener('pointercancel', listenersRef.current.up)
        listenersRef.current = null
      }
      if (draggingRef.current) {
        document.documentElement.style.overflow = ''
        document.body.style.overflow = ''
      }
    }
  }, [])

  // ─── Pointer move handler (attached globally during drag) ──────────────────

  const handlePointerMove = useCallback((e: PointerEvent) => {
    if (pointerIdRef.current !== e.pointerId) return

    const x = e.clientX
    const y = e.clientY

    // Check activation threshold
    if (!activatedRef.current) {
      const dx = x - startPosRef.current.x
      const dy = y - startPosRef.current.y
      if (Math.hypot(dx, dy) < ACTIVATION_DISTANCE) return

      // Threshold crossed — begin drag
      activatedRef.current = true
      draggingRef.current = true

      document.documentElement.style.overflow = 'hidden'
      document.body.style.overflow = 'hidden'

      setDragState(prev => ({
        ...prev,
        isDragging: true,
        dragPosition: { x, y },
      }))
      return
    }

    // Update position and hovered target
    const target = findTargetAtPoint(
      x,
      y,
      boardRef,
      boardRotationRef.current
    )

    setDragState(prev => ({
      ...prev,
      dragPosition: { x, y },
      targetSlot: target,
    }))
  }, [boardRef])

  // ─── Pointer up handler ────────────────────────────────────────────────────

  const handlePointerUp = useCallback((e: PointerEvent) => {
    if (pointerIdRef.current !== e.pointerId) return

    // Remove global listeners
    if (listenersRef.current) {
      window.removeEventListener('pointermove', listenersRef.current.move)
      window.removeEventListener('pointerup', listenersRef.current.up)
      window.removeEventListener('pointercancel', listenersRef.current.up)
      listenersRef.current = null
    }

    // Restore scroll
    document.documentElement.style.overflow = ''
    document.body.style.overflow = ''

    const wasActivated = activatedRef.current
    const source = sourceSlotRef.current

    // Reset all refs
    draggingRef.current = false
    activatedRef.current = false
    pointerIdRef.current = null
    sourceSlotRef.current = null
    cardIdRef.current = null

    if (wasActivated && source) {
      // Determine target at release point
      const target = findTargetAtPoint(
        e.clientX,
        e.clientY,
        boardRef,
        boardRotationRef.current
      )

      setDragState(INITIAL_DRAG_STATE)

      if (target && target !== source) {
        onDragEndRef.current(source, target)
      }
    } else {
      // Threshold never reached — treat as click, just reset
      setDragState(INITIAL_DRAG_STATE)
    }
  }, [boardRef])

  // ─── createDragHandlers ────────────────────────────────────────────────────

  const createDragHandlers = useCallback(
    (slotId: string, cardId: string) => ({
      onPointerDown: (e: React.PointerEvent) => {
        if (disabled) return
        if (e.button !== 0) return   // left click / primary touch only
        if (draggingRef.current) return  // already dragging

        e.preventDefault()

        pointerIdRef.current = e.pointerId
        sourceSlotRef.current = slotId
        cardIdRef.current = cardId
        startPosRef.current = { x: e.clientX, y: e.clientY }
        activatedRef.current = false
        draggingRef.current = false

        setDragState({
          isDragging: false,
          draggedCardId: cardId,
          sourceSlot: slotId,
          targetSlot: null,
          dragPosition: { x: e.clientX, y: e.clientY },
        })

        listenersRef.current = { move: handlePointerMove, up: handlePointerUp }
        window.addEventListener('pointermove', listenersRef.current.move)
        window.addEventListener('pointerup', listenersRef.current.up)
        window.addEventListener('pointercancel', listenersRef.current.up)
      },
    }),
    [disabled, handlePointerMove, handlePointerUp]
  )

  return { dragState, createDragHandlers }
}
