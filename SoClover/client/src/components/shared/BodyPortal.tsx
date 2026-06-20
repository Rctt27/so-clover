import { type ReactNode } from 'react'
import { createPortal } from 'react-dom'

/**
 * Rend ses enfants dans `document.body` via un portal. Utilisé pour les éléments en
 * `position: fixed` (CTA mobile, etc.) qui doivent échapper aux `transform` d'ancêtres
 * (les wrappers de phase animés par Framer créent un contexte de positionnement qui
 * briserait un `position: fixed` rendu à l'intérieur).
 */
export const BodyPortal = ({ children }: { children: ReactNode }) =>
  createPortal(children, document.body)
