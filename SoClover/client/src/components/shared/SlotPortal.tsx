import { useEffect, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

/**
 * Ids des slots du cluster HUD fixe haut-droite (rendus une seule fois dans `App.tsx`).
 * - `mobile-board-controls-slot` : ligne du haut, boutons de rotation (à gauche du chip).
 * - `phase-cta-slot` : ligne du bas, CTA de phase (« Soumettre » / « Valider »).
 * Les phases Écriture/Déduction y projettent leur contenu via `SlotPortal`.
 */
export const MOBILE_BOARD_CONTROLS_SLOT_ID = 'mobile-board-controls-slot'
export const PHASE_CTA_SLOT_ID = 'phase-cta-slot'

/**
 * Projette ses enfants dans le slot `#${slotId}` via un portal. Le slot étant monté par
 * `App.tsx`, il peut ne pas exister au premier rendu : on résout sa référence dans un
 * `useEffect` (après montage du DOM) et on ne rend le portal qu'une fois la cible
 * disponible.
 *
 * Remplace l'ancien couple `MobileBoardControlsPortal` + `BodyPortal` : le cluster HUD
 * est lui-même `position: fixed` (sibling de `<main>`, hors des wrappers de phase animés
 * par Framer), donc le CTA n'a plus besoin d'être porté jusqu'à `document.body` pour
 * échapper aux `transform` d'ancêtres — il vit dans un slot du cluster.
 */
export const SlotPortal = ({ slotId, children }: { slotId: string; children: ReactNode }) => {
  const [slot, setSlot] = useState<HTMLElement | null>(null)

  useEffect(() => {
    setSlot(document.getElementById(slotId))
  }, [slotId])

  return slot ? createPortal(children, slot) : null
}
