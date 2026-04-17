import { useEffect, useRef, useCallback } from 'react'
import { useGameStore, useBoardStore } from '../core/store'
import { gameApi } from '../api/game-api'
import { CONSTANTS } from '../core/constants'
import { validateClueLocally, collectBoardWords } from '../core/clueValidation'

type Position = 'top' | 'right' | 'bottom' | 'left'
const positionToDirection = (p: Position) => p.charAt(0).toUpperCase() + p.slice(1)

export const useClueValidation = (position: Position, clueText: string) => {
  const { gameId, playerId, settings } = useGameStore()
  const { myBoard, setClueValidity } = useBoardStore()

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const requestIdRef = useRef(0)

  const boardWords = myBoard ? collectBoardWords(myBoard.cards) : []

  useEffect(() => {
    const trimmed = clueText.trim()
    const local = validateClueLocally(
      trimmed,
      boardWords,
      settings.language,
      settings.semanticClueCheckEnabled
    )

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
    // boardWords is recomputed each render; stringify to avoid re-runs when identities differ but contents match
  }, [clueText, settings.language, settings.semanticClueCheckEnabled, gameId, playerId, position, setClueValidity, boardWords.join('|')])

  const validateImmediately = useCallback(async (): Promise<boolean> => {
    const trimmed = clueText.trim()
    if (trimmed.length === 0) return true
    if (!gameId || !playerId) return true

    const local = validateClueLocally(trimmed, boardWords, settings.language, settings.semanticClueCheckEnabled)
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
  }, [clueText, gameId, playerId, position, settings.language, settings.semanticClueCheckEnabled, boardWords, setClueValidity])

  return { validateImmediately }
}
