import type { Modifier } from '@dnd-kit/core'

/**
 * Crée un modifier dnd-kit qui centre le DragOverlay sur le curseur quand la carte
 * vient d'un board rotaté.
 *
 * Problème résolu :
 * Quand une carte est dans un conteneur rotaté (le board), les coordonnées retournées
 * par getBoundingClientRect() ne correspondent pas à la position visuelle de la carte
 * à cause des transformations CSS imbriquées. Cela cause un décalage entre le curseur
 * et la carte pendant le drag.
 *
 * Solution :
 * On centre simplement le DragOverlay sur le curseur. La carte "saute" légèrement
 * au début du drag, mais c'est plus robuste que de tenter de compenser des coordonnées
 * incorrectes.
 *
 * @param boardRotation - La rotation actuelle du board en degrés (0, 90, 180, 270)
 */
export const createRotationCompensationModifier = (
  boardRotation: number
): Modifier => {
  // On stocke la position initiale du curseur et l'offset calculé
  let initialOffset: { x: number; y: number } | null = null

  return ({ transform, activatorEvent, active, overlayNodeRect }) => {
    // Récupérer isOutside directement depuis les données de dnd-kit
    const isOutside = active?.data?.current?.isOutside ?? true

    // Pas de compensation si :
    // - Pas de transform
    // - Rotation nulle (pas besoin de compensation)
    // - La carte vient du pool (pas de problème de rotation)
    if (!transform || boardRotation === 0 || isOutside) {
      // Réinitialiser l'offset pour le prochain drag
      initialOffset = null
      return transform
    }

    // Normaliser la rotation à [0, 360)
    const normalizedRotation = ((boardRotation % 360) + 360) % 360
    if (normalizedRotation === 0) {
      initialOffset = null
      return transform
    }

    if (!activatorEvent) {
      return transform
    }

    const pointerEvent = activatorEvent as PointerEvent

    // Calculer l'offset une seule fois au début du drag (quand transform est à 0,0)
    // et le réutiliser pour tout le drag
    if (initialOffset === null && overlayNodeRect) {
      // Position du centre de l'overlay au début du drag
      const overlayCenterX = overlayNodeRect.left + overlayNodeRect.width / 2
      const overlayCenterY = overlayNodeRect.top + overlayNodeRect.height / 2

      // Offset nécessaire pour centrer l'overlay sur le curseur
      initialOffset = {
        x: pointerEvent.clientX - overlayCenterX,
        y: pointerEvent.clientY - overlayCenterY,
      }
    }

    // Appliquer l'offset calculé
    if (initialOffset) {
      return {
        ...transform,
        x: transform.x + initialOffset.x,
        y: transform.y + initialOffset.y,
      }
    }

    return transform
  }
}

/**
 * Modifier simplifié qui centre toujours le DragOverlay sur le curseur.
 * Alternative plus simple mais change légèrement le comportement UX
 * (la carte "saute" au centre du curseur au début du drag).
 */
export const snapCenterToCursor: Modifier = ({
  activatorEvent,
  draggingNodeRect,
  transform,
}) => {
  if (!transform || !activatorEvent || !draggingNodeRect) {
    return transform
  }

  const event = activatorEvent as PointerEvent

  // Calculer l'offset nécessaire pour centrer l'overlay sur le curseur initial
  const centerX = draggingNodeRect.left + draggingNodeRect.width / 2
  const centerY = draggingNodeRect.top + draggingNodeRect.height / 2

  const offsetX = event.clientX - centerX
  const offsetY = event.clientY - centerY

  return {
    ...transform,
    x: transform.x + offsetX,
    y: transform.y + offsetY,
  }
}
