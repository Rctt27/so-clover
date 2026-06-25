import type { TFunction } from 'i18next'
import { ClueValidationError } from './clueValidation'

export const getClueErrorMessage = (error: ClueValidationError, t: TFunction<'writing'>): string => {
  switch (error.rule) {
    case 'ExactMatch': return t('clueError.exactMatch', { word: error.cardWord })
    case 'SimilarStem': return t('clueError.similarStem', { word: error.cardWord })
    case 'TooLong': return t('clueError.tooLong', { max: error.maxLength })
  }
}
