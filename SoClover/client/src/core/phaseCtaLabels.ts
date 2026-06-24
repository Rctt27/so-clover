/**
 * Libellés des CTA de phase, factorisés pour que les variantes desktop (libellés longs)
 * et mobile compacte (un mot) partagent UNE seule échelle de décision. `compact=true`
 * → variante mobile. Le rendu du spinner (« en cours ») reste géré dans le JSX ; ces
 * fonctions ne produisent que du texte.
 */
import type { TFunction } from 'i18next'

export interface WritingSubmitState {
  isSubmitting: boolean
  isSubmitted: boolean
  canSubmit: boolean
  anyChecking: boolean
  allCluesFilled: boolean
}

/** Libellé du bouton « Soumettre » de la phase Écriture. */
export function getWritingSubmitLabel(s: WritingSubmitState, compact: boolean, t: TFunction<'writing'>): string {
  const c = (k: string) => t(`submit.${k}${compact ? '_compact' : ''}` as never)
  if (s.isSubmitting) return c('submitting')
  if (s.isSubmitted) return c('submitted')
  if (s.canSubmit) return c('ready')
  if (s.anyChecking) return t('submit.checking')
  if (s.allCluesFilled) return c('fix')
  return c('missing')
}

/** Libellé du bouton « Valider / Plateau suivant » de la phase Déduction. */
export function getGuessingValidateLabel(canMoveToNext: boolean, compact: boolean, t: TFunction<'guessing'>): string {
  const k = canMoveToNext ? 'next' : 'validate'
  return t(`validate.${k}${compact ? '_compact' : ''}` as never)
}
