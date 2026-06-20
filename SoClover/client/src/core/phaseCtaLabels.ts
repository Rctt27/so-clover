/**
 * Libellés des CTA de phase, factorisés pour que les variantes desktop (libellés longs)
 * et mobile compacte (un mot) partagent UNE seule échelle de décision. `compact=true`
 * → variante mobile. Le rendu du spinner (« en cours ») reste géré dans le JSX ; ces
 * fonctions ne produisent que du texte.
 */

export interface WritingSubmitState {
  isSubmitting: boolean
  isSubmitted: boolean
  canSubmit: boolean
  anyChecking: boolean
  allCluesFilled: boolean
}

/** Libellé du bouton « Soumettre » de la phase Écriture. */
export function getWritingSubmitLabel(s: WritingSubmitState, compact: boolean): string {
  if (s.isSubmitting) return compact ? 'Soumission…' : 'Soumission...'
  if (s.isSubmitted) return compact ? 'Soumis ✓' : 'Plateau Soumis ✓'
  if (s.canSubmit) return compact ? 'Soumettre' : 'Soumettre le plateau'
  if (s.anyChecking) return 'Vérification…'
  if (s.allCluesFilled) return compact ? 'Corrigez' : 'Corrigez les indices'
  return compact ? 'Indices manquants' : 'Saisissez les 4 indices'
}

/** Libellé du bouton « Valider / Plateau suivant » de la phase Déduction. */
export function getGuessingValidateLabel(canMoveToNext: boolean, compact: boolean): string {
  if (canMoveToNext) return compact ? 'Suivant' : 'Plateau suivant'
  return compact ? 'Valider' : 'Valider le plateau'
}
