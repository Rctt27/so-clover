/**
 * Seuil d'activation d'un drag de carte (déplacement / swap), exprimé en pixels de
 * déplacement du pointeur depuis le pointerdown. Sous ce seuil, le geste reste un clic/tap
 * (utile au mode clic-clic tablette) ; au-delà, on bascule en drag.
 *
 * Le seuil est **dépendant du type de pointeur** : un doigt « jitte » bien plus qu'une souris
 * ou un stylet. Un seuil unique trop bas transformait un clic sur le coin (pour tourner) en
 * swap accidentel avec une carte voisine dès quelques pixels de tremblement involontaire.
 */
export const DRAG_ACTIVATION_DISTANCE = {
  /** Souris / stylet : pointeurs précis, seuil bas. */
  mouse: 8,
  /** Tactile : seuil plus haut pour absorber le jitter du doigt. */
  touch: 16,
} as const

/**
 * Distance d'activation (px) à appliquer pour un type de pointeur donné.
 * Tout pointeur non tactile (mouse, pen, ou valeur inconnue/vide) utilise le seuil précis.
 */
export function activationDistanceForPointer(pointerType: string): number {
  return pointerType === 'touch'
    ? DRAG_ACTIVATION_DISTANCE.touch
    : DRAG_ACTIVATION_DISTANCE.mouse
}

/**
 * Vrai si un déplacement (dx, dy) depuis le point de départ suffit à activer un drag,
 * compte tenu du type de pointeur. Faux → le geste reste un clic/tap.
 */
export function isDragActivated(dx: number, dy: number, pointerType: string): boolean {
  return Math.hypot(dx, dy) >= activationDistanceForPointer(pointerType)
}
