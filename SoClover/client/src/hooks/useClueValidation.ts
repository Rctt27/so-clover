import { useEffect, useRef, useCallback, useMemo } from 'react'
import { useGameStore, useBoardStore } from '../core/store'
import { gameApi } from '../api/game-api'
import { CONSTANTS } from '../core/constants'
import { computeLocalClueValidity, collectBoardWords } from '../core/clueValidation'

type Position = 'top' | 'right' | 'bottom' | 'left'
const positionToDirection = (p: Position) => p.charAt(0).toUpperCase() + p.slice(1)

/**
 * @param isEditable la vérification sémantique ne s'exécute QUE lorsque l'input est éditable
 *   (phase WritingClues). En lecture seule (Guessing/Scoring), aucune validation n'est lancée.
 */
export const useClueValidation = (position: Position, clueText: string, isEditable = true) => {
  const { gameId, playerId, settings } = useGameStore()
  const { myBoard, setClueValidity } = useBoardStore()

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)

  const boardWordsKey = myBoard ? collectBoardWords(myBoard.cards).join('|') : ''
  const boardWords = useMemo(() => boardWordsKey ? boardWordsKey.split('|') : [], [boardWordsKey])

  useEffect(() => {
    return () => { abortRef.current?.abort() }
  }, [])

  useEffect(() => {
    const trimmed = clueText.trim()
    const local = computeLocalClueValidity(
      isEditable,
      trimmed,
      boardWords,
      settings.language,
      settings.semanticClueCheckEnabled
    )

    // Lecture seule (Guessing/Scoring) → on ne touche pas à la validité ni au serveur.
    if (local === null) return

    setClueValidity(position, { ...local, isChecking: !local.isValid ? false : trimmed.length > 0 })

    if (trimmed.length === 0 || !gameId || !playerId) return

    // Local already found errors → skip server call (server will confirm on blur)
    if (!local.isValid) return

    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(async () => {
      const myRequestId = ++requestIdRef.current
      abortRef.current?.abort()
      const ctrl = new AbortController()
      abortRef.current = ctrl

      try {
        const resp = await gameApi.validateClue(
          gameId,
          playerId,
          positionToDirection(position),
          trimmed,
          ctrl.signal
        )
        if (myRequestId !== requestIdRef.current) return // outdated response
        setClueValidity(position, {
          isValid: resp.isValid,
          errors: resp.errors,
          isChecking: false,
        })
      } catch (err) {
        if ((err as Error).name === 'AbortError') return
        // Network failure → leave optimistic result in place, clear isChecking
        setClueValidity(position, { ...local, isChecking: false })
      }
    }, CONSTANTS.CLUE_VALIDATION.debounceMs)

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current)
    }
  }, [clueText, settings.language, settings.semanticClueCheckEnabled, gameId, playerId, position, setClueValidity, boardWordsKey, isEditable])

  const validateImmediately = useCallback(async (): Promise<boolean> => {
    const trimmed = clueText.trim()
    if (trimmed.length === 0) return true
    if (!gameId || !playerId) return true

    const local = computeLocalClueValidity(isEditable, trimmed, boardWords, settings.language, settings.semanticClueCheckEnabled)
    if (local === null) return true // lecture seule : pas de validation
    if (!local.isValid) {
      setClueValidity(position, { ...local, isChecking: false })
      return false
    }

    try {
      const resp = await gameApi.validateClue(gameId, playerId, positionToDirection(position), trimmed)
      setClueValidity(position, { isValid: resp.isValid, errors: resp.errors, isChecking: false })
      return resp.isValid
    } catch {
      return local.isValid
    }
  }, [clueText, gameId, playerId, position, settings.language, settings.semanticClueCheckEnabled, boardWordsKey, setClueValidity, isEditable])

  return { validateImmediately }
}
