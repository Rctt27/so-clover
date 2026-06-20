/**
 * Détection « device à pointeur grossier » (tactile) côté JS.
 *
 * Le reste de l'app gate déjà l'ergonomie tactile via la média-query CSS
 * `(pointer: coarse)` (cibles 44px, layout paysage, etc. — cf. Axe 4). Certaines
 * fonctions n'ont pas d'équivalent CSS et doivent décider en JS : le mouse tracking
 * (écouter `pointermove` plutôt que `mousemove`) et le tooltip d'explication des
 * indices (tap plutôt que hover). On réutilise donc la même média-query, mais lue
 * en JavaScript, pour rester cohérent avec le gating CSS existant.
 */
export const COARSE_POINTER_QUERY = '(pointer: coarse)'

type MediaQueryListLike = {
  matches: boolean
  addEventListener?: (type: 'change', listener: () => void) => void
  removeEventListener?: (type: 'change', listener: () => void) => void
}

type MatchMedia = (query: string) => MediaQueryListLike

/**
 * Cœur pur et testable : évalue la média-query coarse-pointer via la fonction
 * `matchMedia` fournie. Sûr si `matchMedia` est absent (SSR / vieux runtime) → `false`.
 */
export function evaluateCoarsePointer(matchMedia: MatchMedia | undefined): boolean {
  if (typeof matchMedia !== 'function') {
    return false
  }
  return matchMedia(COARSE_POINTER_QUERY).matches
}

/**
 * Vrai si le pointeur principal du device est « grossier » (doigt) plutôt que précis
 * (souris/stylet). S'appuie sur `matchMedia('(pointer: coarse)')`.
 */
export function isCoarsePointer(): boolean {
  if (typeof window === 'undefined') {
    return false
  }
  return evaluateCoarsePointer(window.matchMedia?.bind(window))
}

/**
 * S'abonne aux changements de type de pointeur via la fonction `matchMedia` fournie.
 * Renvoie une fonction de désabonnement. Cœur pur de `useCoarsePointer` (testable hors
 * React / hors DOM). No-op sûr si `matchMedia` est absent ou si le MediaQueryList
 * n'expose pas `addEventListener` (vieux Safari).
 */
export function subscribeCoarsePointer(
  matchMedia: MatchMedia | undefined,
  onChange: () => void,
): () => void {
  if (typeof matchMedia !== 'function') {
    return () => {}
  }
  const mql = matchMedia(COARSE_POINTER_QUERY)
  mql.addEventListener?.('change', onChange)
  return () => mql.removeEventListener?.('change', onChange)
}
