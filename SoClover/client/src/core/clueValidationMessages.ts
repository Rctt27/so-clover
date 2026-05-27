import { ClueValidationError } from './clueValidation'

export const getClueErrorMessage = (error: ClueValidationError): string => {
    switch (error.rule) {
        case 'ExactMatch':
            return `Votre indice reprend le mot « ${error.cardWord} » de votre plateau`
        case 'SimilarStem':
            return `Votre indice est trop proche du mot « ${error.cardWord} » de votre plateau`
    }
}

export const LOBBY_SEMANTIC_TOGGLE_LABEL = 'Contrôle sémantique des indices'
export const LOBBY_SEMANTIC_TOGGLE_TOOLTIP_DISABLED = 'Disponible uniquement avec les dictionnaires Français et Anglais'

export const LOBBY_GUESS_AI_BOARD_ONLY_LABEL = 'Deviner uniquement les plateaux IA'
export const LOBBY_GUESS_AI_BOARD_ONLY_TOOLTIP_DISABLED = 'Disponible uniquement si au moins un joueur IA est dans la partie'