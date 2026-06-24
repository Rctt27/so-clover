import { ClueValidationError } from './clueValidation'

export const getClueErrorMessage = (error: ClueValidationError): string => {
    switch (error.rule) {
        case 'ExactMatch':
            return `Votre indice reprend le mot « ${error.cardWord} » de votre plateau`
        case 'SimilarStem':
            return `Votre indice est trop proche du mot « ${error.cardWord} » de votre plateau`
    }
}

