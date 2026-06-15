import { useEffect, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

/**
 * Id du slot HUD (rendu une seule fois dans `App.tsx`, à gauche du chip de
 * connexion) qui accueille les contrôles de plateau projetés par les phases sur
 * mobile (rotation). Sur desktop le slot reste vide.
 */
export const MOBILE_BOARD_CONTROLS_SLOT_ID = 'mobile-board-controls-slot'

/**
 * Projette ses enfants dans le slot HUD `#mobile-board-controls-slot` via un
 * portal. Les phases Writing/Guessing s'en servent (sous `pointer:coarse`) pour
 * remonter les boutons de rotation dans le cluster HUD haut, hors du flux du
 * plateau height-bound.
 *
 * Le slot étant monté par `App.tsx`, il peut ne pas exister au premier rendu :
 * on résout sa référence dans un `useEffect` (après montage du DOM) et on ne
 * rend le portal qu'une fois la cible disponible.
 */
export const MobileBoardControlsPortal = ({ children }: { children: ReactNode }) => {
  const [slot, setSlot] = useState<HTMLElement | null>(null)

  useEffect(() => {
    setSlot(document.getElementById(MOBILE_BOARD_CONTROLS_SLOT_ID))
  }, [])

  return slot ? createPortal(children, slot) : null
}
