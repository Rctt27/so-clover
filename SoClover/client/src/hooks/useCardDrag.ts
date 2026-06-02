import { useRef, useState, useCallback, useEffect } from 'react'
import { LOGICAL_SLOTS } from '../core/utils'
import { useGuessingStore } from '../core/store'
import { isDragActivated } from '../core/dragActivation'

/** Fenêtre post-drop pendant laquelle on continue à supprimer les animations
 *  locales — couvre le SignalR roundtrip + le re-render qui suit. */
const LOCAL_DRAG_SUPPRESSION_TTL_MS = 500

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

// Le seuil d'activation est désormais dépendant du type de pointeur (souris/stylet vs tactile) :
// voir `core/dragActivation.ts`. Un seuil unique trop bas transformait un micro-jitter tactile en
// swap accidentel lors d'un clic sur un coin de rotation.

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
  const pointerTypeRef = useRef<string>('mouse')  // type de pointeur du drag courant (mouse/pen/touch)
  const startPosRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 })
  const boardRotationRef = useRef(boardRotationDeg)
  const onDragEndRef = useRef(onDragEnd)
  const listenersRef = useRef<{ move: (e: PointerEvent) => void; up: (e: PointerEvent) => void } | null>(null)
  const suppressionTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

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
      if (suppressionTimerRef.current !== null) {
        clearTimeout(suppressionTimerRef.current)
        suppressionTimerRef.current = null
      }
      useGuessingStore.getState().setLocalDragActive(false)
    }
  }, [])

  // ─── Pointer move handler (attached globally during drag) ──────────────────

  const handlePointerMove = useCallback((e: PointerEvent) => {
    if (pointerIdRef.current !== e.pointerId) return

    const x = e.clientX
    const y = e.clientY

    // Check activation threshold (seuil dépendant du type de pointeur)
    if (!activatedRef.current) {
      const dx = x - startPosRef.current.x
      const dy = y - startPosRef.current.y
      if (!isDragActivated(dx, dy, pointerTypeRef.current)) return

      // Threshold crossed — begin drag.
      // preventDefault est appelé ICI (et non sur pointerdown) pour qu'un simple tap —
      // qui ne franchit jamais le seuil — laisse passer l'événement `click` natif.
      // C'est ce click qui pilote le mode clic-clic (tablette). Bloquer le default dès
      // le franchissement du seuil suffit à inhiber le click parasite d'un vrai drag.
      e.preventDefault()
      activatedRef.current = true
      draggingRef.current = true

      // Annule un éventuel reset de fin-de-drag précédent et signale au store
      // que l'utilisateur initie une action drag locale.
      if (suppressionTimerRef.current !== null) {
        clearTimeout(suppressionTimerRef.current)
        suppressionTimerRef.current = null
      }
      useGuessingStore.getState().setLocalDragActive(true)

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
    const cardIdSnapshot = cardIdRef.current

    // Reset all refs
    draggingRef.current = false
    activatedRef.current = false
    pointerIdRef.current = null
    sourceSlotRef.current = null
    cardIdRef.current = null

    if (wasActivated && source) {
      // Snapshot of the cardId BEFORE refs were nulled out above.
      // Conservé pour que le couple (sourceSlot, draggedCardId) reste matchable
      // dans le slot d'origine pendant la fenêtre de suppression.
      const draggedCardId = cardIdSnapshot

      // Determine target at release point
      const target = findTargetAtPoint(
        e.clientX,
        e.clientY,
        boardRef,
        boardRotationRef.current
      )

      // On NE reset PAS complètement le dragState : on conserve draggedCardId et
      // sourceSlot pour que `isDragSource` (qui exige slot+cardId match côté
      // Board/Pool) garde la carte cachée dans son slot d'origine jusqu'à ce que
      // SignalR ait propagé l'update (le slot d'origine perdra alors la carte et
      // le match s'évalue à false naturellement).
      // isDragging passe à false → l'overlay flottant se démonte normalement.
      setDragState({
        isDragging: false,
        draggedCardId,
        sourceSlot: source,
        targetSlot: null,
        dragPosition: { x: 0, y: 0 },
      })

      if (target && target !== source) {
        onDragEndRef.current(source, target)
      }

      // Maintient la suppression des animations + masquage du slot d'origine le
      // temps que le state serveur se propage. Au-delà, on libère et on reset
      // complètement le dragState.
      if (suppressionTimerRef.current !== null) {
        clearTimeout(suppressionTimerRef.current)
      }
      suppressionTimerRef.current = setTimeout(() => {
        useGuessingStore.getState().setLocalDragActive(false)
        setDragState(INITIAL_DRAG_STATE)
        suppressionTimerRef.current = null
      }, LOCAL_DRAG_SUPPRESSION_TTL_MS)
    } else {
      // Threshold never reached — treat as click, just reset
      setDragState(INITIAL_DRAG_STATE)
      // Aucun drag réel n'a eu lieu : libère immédiatement le flag s'il
      // avait été (improbablement) set.
      useGuessingStore.getState().setLocalDragActive(false)
    }
  }, [boardRef])

  // ─── createDragHandlers ────────────────────────────────────────────────────

  const createDragHandlers = useCallback(
    (slotId: string, cardId: string) => ({
      onPointerDown: (e: React.PointerEvent) => {
        if (disabled) return
        if (e.button !== 0) return   // left click / primary touch only
        if (draggingRef.current) return  // already dragging

        // Pas de preventDefault ici : on le diffère à l'activation du drag (cf. handlePointerMove)
        // afin de préserver le click natif sur un simple tap (mode clic-clic tablette).

        // Annule le reset différé d'un drag précédent : sinon il pourrait nuker
        // le state du nouveau drag pendant son déroulé.
        if (suppressionTimerRef.current !== null) {
          clearTimeout(suppressionTimerRef.current)
          suppressionTimerRef.current = null
        }

        pointerIdRef.current = e.pointerId
        pointerTypeRef.current = e.pointerType || 'mouse'
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
