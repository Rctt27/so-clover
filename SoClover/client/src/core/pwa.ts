/**
 * Détection PWA / standalone et éligibilité au hint « Ajouter à l'écran d'accueil » (iOS).
 *
 * iOS n'expose AUCUNE invite d'installation programmatique (pas de `beforeinstallprompt`).
 * Le seul vrai plein écran sur iPhone passe par l'ajout à l'écran d'accueil → lancement en
 * mode standalone (sans barre d'URL ni onglets). Ce module décide, en JS pur et testable,
 * quand afficher le hint qui guide l'utilisateur vers cette manip.
 *
 * Pattern calqué sur core/coarsePointer.ts : cœurs purs à dépendances injectées + fins
 * wrappers globaux gardés (typeof window/navigator) pour SSR / env de test node.
 */

export const STANDALONE_QUERY = '(display-mode: standalone)'
export const A2HS_DISMISSED_KEY = 'so-clover-a2hs-dismissed'

type MediaQueryListLike = { matches: boolean }
type MatchMedia = (query: string) => MediaQueryListLike

/** Vrai si la page tourne en standalone (PWA installée). Gère le `navigator.standalone`
 *  propriétaire iOS ET la média-query standard `(display-mode: standalone)`. */
export function evaluateStandalone(
  matchMedia: MatchMedia | undefined,
  navigatorStandalone: boolean | undefined,
): boolean {
  if (navigatorStandalone === true) return true
  if (typeof matchMedia !== 'function') return false
  return matchMedia(STANDALONE_QUERY).matches
}

/** Vrai si l'UA correspond à un appareil iOS (iPhone/iPod/iPad). */
export function evaluateIsIOS(userAgent: string | undefined): boolean {
  if (!userAgent) return false
  return /iphone|ipod|ipad/i.test(userAgent)
}

/** Vrai si le hint A2HS a déjà été rejeté (persisté). Sûr si storage absent / mode privé. */
export function isA2HSDismissed(storage: Pick<Storage, 'getItem'> | undefined): boolean {
  if (!storage) return false
  try {
    return storage.getItem(A2HS_DISMISSED_KEY) === 'true'
  } catch {
    return false
  }
}

/** Persiste le rejet du hint A2HS. No-op sûr si storage absent / quota dépassé. */
export function setA2HSDismissed(storage: Pick<Storage, 'setItem'> | undefined): void {
  if (!storage) return
  try {
    storage.setItem(A2HS_DISMISSED_KEY, 'true')
  } catch {
    /* mode privé / quota : ignorer */
  }
}

/** Cœur pur : faut-il afficher le hint ? iOS, pas déjà standalone, pas rejeté. */
export function evaluateShouldShowA2HSHint(params: {
  isIOS: boolean
  isStandalone: boolean
  isDismissed: boolean
}): boolean {
  return params.isIOS && !params.isStandalone && !params.isDismissed
}

// ─── Wrappers globaux gardés (lus paresseusement, sûrs hors navigateur) ──────────

export function isStandalone(): boolean {
  if (typeof window === 'undefined') return false
  const nav = window.navigator as Navigator & { standalone?: boolean }
  return evaluateStandalone(window.matchMedia?.bind(window), nav?.standalone)
}

export function isIOS(): boolean {
  if (typeof navigator === 'undefined') return false
  return evaluateIsIOS(navigator.userAgent)
}

export function shouldShowA2HSHint(): boolean {
  return evaluateShouldShowA2HSHint({
    isIOS: isIOS(),
    isStandalone: isStandalone(),
    isDismissed: isA2HSDismissed(typeof localStorage === 'undefined' ? undefined : localStorage),
  })
}

export function dismissA2HSHint(): void {
  setA2HSDismissed(typeof localStorage === 'undefined' ? undefined : localStorage)
}
