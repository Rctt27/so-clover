/**
 * Décide si un indice doit être persisté côté serveur automatiquement, sans attendre
 * le blur de l'input. Objectif : dès que les 4 indices sont enregistrés par le serveur,
 * le bouton « Soumettre le plateau » s'active dynamiquement — le joueur n'a pas à faire
 * un clic supplémentaire pour quitter le champ.
 *
 * On ne persiste que lorsque la validation sémantique (debouncée, côté serveur) a
 * confirmé l'indice (`isValid && !isChecking`), qu'il est non vide et qu'il diffère de
 * la valeur déjà enregistrée (anti re-POST). La persistance reste bloquée en lecture
 * seule (`disabled` — phases Guessing/Scoring).
 */
export interface AutoPersistClueState {
  /** Valeur en cours de saisie dans l'input (état local). */
  localValue: string
  /** Valeur déjà enregistrée côté serveur (`myBoard.clues[position].text`). */
  savedValue: string
  /** Validité sémantique confirmée pour `localValue`. */
  isValid: boolean
  /** Une vérification serveur est encore en vol pour `localValue`. */
  isChecking: boolean
  /** Input en lecture seule (hors phase d'écriture). */
  disabled: boolean
}

export const shouldAutoPersistClue = ({
  localValue,
  savedValue,
  isValid,
  isChecking,
  disabled,
}: AutoPersistClueState): boolean => {
  if (disabled) return false
  if (isChecking || !isValid) return false
  const trimmed = localValue.trim()
  if (trimmed === '') return false
  return trimmed !== savedValue.trim()
}
