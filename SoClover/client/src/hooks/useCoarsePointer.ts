import { useSyncExternalStore } from 'react'
import { evaluateCoarsePointer, subscribeCoarsePointer } from '../core/coarsePointer'

/** Résout `window.matchMedia` lié, ou `undefined` hors navigateur (SSR / test node). */
const getMatchMedia = () =>
  typeof window !== 'undefined' ? window.matchMedia?.bind(window) : undefined

/**
 * Vrai si le pointeur principal du device est « grossier » (doigt). RÉACTIF : se met à
 * jour si le type de pointeur change à l'exécution (tablette + souris BT, bascule device
 * DevTools, 2-en-1). Remplace `useState(isCoarsePointer)` qui figeait la valeur au montage
 * et pouvait diverger des règles CSS `(pointer: coarse)`. Construit sur `useSyncExternalStore`
 * au-dessus du cœur pur testé dans `core/coarsePointer.ts`.
 */
export function useCoarsePointer(): boolean {
  return useSyncExternalStore(
    (onStoreChange) => subscribeCoarsePointer(getMatchMedia(), onStoreChange),
    () => evaluateCoarsePointer(getMatchMedia()),
    () => false,
  )
}
