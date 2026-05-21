import React, { useLayoutEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { motion, AnimatePresence } from 'framer-motion'
import { CONSTANTS } from '../../../core/constants'

interface ClueExplanationTooltipProps {
  explanation: string
  visible: boolean
  /** The element the tooltip should anchor to (the clue input). Used to read its
   *  on-screen bounding box, so the tooltip placement is stable regardless of
   *  any CSS rotation applied to ancestor layers (e.g. the Board's rotating layer). */
  anchorRef: React.RefObject<HTMLElement | null>
}

const { maxWidthPx, fadeDurationSec, offsetPx, viewportMarginPx, zIndex } = CONSTANTS.CLUE_EXPLANATION_TOOLTIP

interface FixedPlacement {
  top: number
  left: number
}

// Place the tooltip above the clue's screen rect when possible, fall back to below
// if the clue sits too close to the top edge. Horizontally centered on the clue and
// clamped to the viewport so the bubble never gets clipped at the edges.
const computePlacement = (rect: DOMRect, tooltipWidth: number, tooltipHeight: number): FixedPlacement => {
  const centerX = rect.left + rect.width / 2
  let left = centerX - tooltipWidth / 2
  left = Math.max(viewportMarginPx, Math.min(left, window.innerWidth - tooltipWidth - viewportMarginPx))

  const spaceAbove = rect.top
  const placeBelow = spaceAbove < tooltipHeight + offsetPx + viewportMarginPx
  const top = placeBelow
    ? rect.bottom + offsetPx
    : rect.top - tooltipHeight - offsetPx

  return { top, left }
}

export const ClueExplanationTooltip: React.FC<ClueExplanationTooltipProps> = ({
  explanation,
  visible,
  anchorRef,
}) => {
  const [placement, setPlacement] = useState<FixedPlacement | null>(null)
  const [tooltipEl, setTooltipEl] = useState<HTMLDivElement | null>(null)

  // Re-measure each time the tooltip becomes visible: anchor rect can change between
  // hovers (board rotation, layout shift). We do it in useLayoutEffect so the first
  // paint already has the correct fixed position, avoiding a one-frame flash at (0,0).
  useLayoutEffect(() => {
    if (!visible) {
      setPlacement(null)
      return
    }
    const anchor = anchorRef.current
    if (!anchor) return

    const rect = anchor.getBoundingClientRect()
    // Use the actual rendered size if available (after the first measure), else fall
    // back to the configured max width and a small initial height estimate.
    const width = tooltipEl?.offsetWidth ?? Math.min(maxWidthPx, window.innerWidth - 2 * viewportMarginPx)
    const height = tooltipEl?.offsetHeight ?? 0
    setPlacement(computePlacement(rect, width, height))
  }, [visible, anchorRef, tooltipEl])

  return createPortal(
    <AnimatePresence>
      {visible && placement && (
        <motion.div
          ref={setTooltipEl}
          role="tooltip"
          initial={{ opacity: 0, y: -4 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          transition={{ duration: fadeDurationSec }}
          style={{
            position: 'fixed',
            top: placement.top,
            left: placement.left,
            maxWidth: `${maxWidthPx}px`,
            zIndex,
            pointerEvents: 'none',
          }}
          className="px-3 py-2 rounded-lg shadow-lg bg-white/95 text-gray-800 text-sm leading-snug border border-gray-200"
        >
          {explanation}
        </motion.div>
      )}
    </AnimatePresence>,
    document.body
  )
}
