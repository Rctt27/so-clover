import { ClueValidationError } from './clueValidation'

export const getClueErrorMessage = (error: ClueValidationError): string => {
    switch (error.rule) {
        case 'ExactMatch':
            return `Votre indice reprend le mot « ${error.cardWord} » de votre plateau`
        case 'SimilarStem':
            return `Votre indice est trop proche du mot "${error.cardWord}" de votre plateau`
    }
}

export const LOBBY_SEMANTIC_TOGGLE_LABEL = 'Contrôle sémantique des indices'
export const LOBBY_SEMANTIC_TOGGLE_TOOLTIP_DISABLED = 'Disponible uniquement avec le dictionnaire Français'