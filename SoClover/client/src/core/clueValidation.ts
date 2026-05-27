export type ClueValidationRule = 'ExactMatch' | 'SimilarStem'

export interface ClueValidationError {
    rule: ClueValidationRule
    cardWord: string
    conflictingDirection?: 'Top' | 'Right' | 'Bottom' | 'Left' | null
}

export interface ClueValidationResult {
    isValid: boolean
    errors: ClueValidationError[]
}

const MIN_WORD_LENGTH = 3
const VOYELLES = new Set(['a', 'e', 'i', 'o', 'u', 'y'])

export const normalizeText = (input: string | null | undefined): string => {
    if (!input) return ''
    const withLigatures = input
        .trim()
        .toLowerCase()
        .replace(/œ/g, 'oe')
        .replace(/æ/g, 'ae')
        .replace(/[\u2018\u2019]/g, "'")
    return withLigatures.normalize('NFD').replace(/\p{Mn}/gu, '').normalize('NFC')
}

export const isFrenchLanguage = (language: string): boolean =>
    normalizeText(language).startsWith('francais')

// Languages whose dictionaries support semantic clue conformity validation.
// Keep in sync with the backend SemanticValidationSupport.
const SEMANTIC_SUPPORTED_PREFIXES = ['francais', 'english']

export const supportsSemanticCheck = (language: string): boolean => {
    const norm = normalizeText(language)
    return SEMANTIC_SUPPORTED_PREFIXES.some(prefix => norm.startsWith(prefix))
}

export const validateClueLocally = (
    clueText: string,
    boardWords: string[],
    language: string,
    semanticCheckEnabled: boolean
): ClueValidationResult => {
    if (!semanticCheckEnabled || !supportsSemanticCheck(language))
        return { isValid: true, errors: [] }

    const clueNorm = normalizeText(clueText)
    if (clueNorm.length === 0)
        return { isValid: true, errors: [] }

    const errors: ClueValidationError[] = []
    // R2 (vowel stem) is a French morphology heuristic — not applied to other languages.
    const applyStemRule = isFrenchLanguage(language)

    for (const word of boardWords) {
        const wordNorm = normalizeText(word)
        if (wordNorm.length < MIN_WORD_LENGTH) continue

        if (clueNorm.includes(wordNorm) || (clueNorm.length >= MIN_WORD_LENGTH && wordNorm.includes(clueNorm))) {
            errors.push({ rule: 'ExactMatch', cardWord: word })
            continue
        }

        if (!applyStemRule) continue

        const lastChar = wordNorm[wordNorm.length - 1]
        if (!VOYELLES.has(lastChar)) continue

        const stem = wordNorm.slice(0, -1)
        if (stem.length < MIN_WORD_LENGTH) continue

        if (clueNorm.includes(stem) || (clueNorm.length >= MIN_WORD_LENGTH && stem.includes(clueNorm))) {
            errors.push({ rule: 'SimilarStem', cardWord: word })
        }
    }

    return { isValid: errors.length === 0, errors }
}

export const collectBoardWords = (cards: Array<{ words: string[] } | null>): string[] => {
    const out: string[] = []
    for (const card of cards) {
        if (!card) continue
        for (const w of card.words) {
            if (w && w.trim().length > 0) out.push(w)
        }
    }
    return out
}