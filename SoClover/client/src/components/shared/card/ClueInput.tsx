import React, { useState, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { CONSTANTS } from '../../../core/constants'
import { computeBoardGeometry, getCluePlacement } from '../../../core/boardGeometry'
import { useClueValidation } from '../../../hooks/useClueValidation'
import { useBoardStore } from '../../../core/store'
import { getClueErrorMessage } from '../../../core/clueValidationMessages'
import { ClueValidationRejection } from '../../../types/game'
import { debugLog } from '../../../core/debug'
import { isCoarsePointer } from '../../../core/coarsePointer'
import { ClueExplanationTooltip } from './ClueExplanationTooltip'

export type ClueStatus = 'idle' | 'saving' | 'success' | 'error'

interface ClueInputProps {
  position: 'top' | 'right' | 'bottom' | 'left'
  value: string
  onSave: (value: string) => Promise<void>
  disabled?: boolean
  /** LLM-generated rationale for AI clues. Server-gated: only set when the current
   *  Guessing board has been resolved (success or attempts exhausted). When present
   *  and the input is disabled (read-only display), a hover tooltip becomes available. */
  explanation?: string | null
}

export const ClueInput: React.FC<ClueInputProps> = ({ position, value, onSave, disabled, explanation }) => {
  // Visibilité du tooltip d'explication : pilotée par le hover sur desktop, par un
  // tap (bouton info) sur device tactile où le hover n'existe pas (cf. Axe 5 mobile).
  const [isTooltipVisible, setIsTooltipVisible] = useState(false)
  // Le type de pointeur ne change pas à l'exécution → on l'évalue une seule fois.
  const [isCoarse] = useState(isCoarsePointer)
  const [localValue, setLocalValue] = useState(value)
  const [status, setStatus] = useState<ClueStatus>('idle')
  const inputRef = useRef<HTMLInputElement>(null)
  const clueAnchorRef = useRef<HTMLDivElement>(null)

  // La vérification sémantique n'est pertinente qu'en édition (phase WritingClues).
  // En lecture seule (Guessing/Scoring), `disabled` est vrai → on ne valide pas.
  const { validateImmediately } = useClueValidation(position, localValue, !disabled)
  const validity = useBoardStore((s) => s.clueValidity[position])

  const boardGeo = computeBoardGeometry(CONSTANTS.ASSET_REFERENCES.board)
  const theme = CONSTANTS.THEME_CONFIG

  useEffect(() => {
    if (document.activeElement !== inputRef.current) {
      setLocalValue(value)
    }
  }, [value])

  const handleSave = async () => {
    const trimmed = localValue.trim()
    if (trimmed === value || (trimmed === '' && value === '')) {
      setStatus('idle')
      return
    }

    const ok = await validateImmediately()
    if (!ok) {
      setStatus('error')
      return
    }

    setStatus('saving')
    try {
      await onSave(trimmed)
      setStatus('success')
      setTimeout(() => setStatus('idle'), 2000)
    } catch (error) {
      if (error instanceof ClueValidationRejection) {
        setStatus('error')
        return
      }
      debugLog('ClueInput', 'Save failed', error)
      setStatus('error')
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') inputRef.current?.blur()
  }

  // Tactile : à l'ouverture du clavier virtuel (phase Writing), recadrer l'indice focus
  // au centre pour qu'il ne reste pas masqué par le clavier en layout 100svh. On laisse
  // le clavier s'animer avant de scroller. Le focus n'arrive qu'en édition (input non
  // disabled) → naturellement limité à la phase d'écriture.
  const handleFocus = () => {
    if (!isCoarse) return
    window.setTimeout(() => {
      clueAnchorRef.current?.scrollIntoView({ block: 'center', behavior: 'smooth' })
    }, 300)
  }

  const getPositionStyle = () => {
    // Sous pointeur tactile : champ dé-pivoté (texte horizontal) et élargi pour ne
    // plus tronquer les mots longs (cf. getCluePlacement). Desktop : inchangé.
    const { topPct, leftPct, rotation, widthPct } = getCluePlacement(boardGeo, position, isCoarse)
    return {
      position: 'absolute' as const,
      top: `${topPct}%`,
      left: `${leftPct}%`,
      width: `${widthPct}%`,
      transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
      zIndex: 100,
    }
  }

  const hasValidationError = !validity.isValid && localValue.trim().length > 0

  const getBorderColor = () => {
    if (status === 'saving') return theme.clueBorderColorSaving
    if (status === 'error' || hasValidationError) return theme.clueBorderColorError
    const isDirty = localValue.trim() !== value.trim()
    if (isDirty && status === 'idle') return theme.clueBorderColorSaving
    return theme.clueBorderColor
  }

  const shakeAnimation = { x: [0, -10, 10, -10, 10, 0], transition: { duration: 0.4 } }

  const firstError = validity.errors[0]
  const errorMessageId = `clue-error-${position}`

  const explanationIsAvailable = !!disabled && !!explanation

  // Tactile : fermer le tooltip ouvert au tap quand on tape en dehors de l'indice.
  useEffect(() => {
    if (!isCoarse || !isTooltipVisible) return
    const handlePointerDown = (e: PointerEvent) => {
      if (!clueAnchorRef.current?.contains(e.target as Node)) {
        setIsTooltipVisible(false)
      }
    }
    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [isCoarse, isTooltipVisible])

  return (
    <div data-clue-wrapper style={getPositionStyle()}>
    <motion.div
      ref={clueAnchorRef}
      animate={status === 'error' ? shakeAnimation : {}}
      className="w-full relative"
      onMouseEnter={explanationIsAvailable && !isCoarse ? () => setIsTooltipVisible(true) : undefined}
      onMouseLeave={explanationIsAvailable && !isCoarse ? () => setIsTooltipVisible(false) : undefined}
    >
      <input
        ref={inputRef}
        type="text"
        value={localValue}
        onChange={(e) => setLocalValue(e.target.value)}
        onBlur={handleSave}
        onKeyDown={handleKeyDown}
        onFocus={handleFocus}
        disabled={disabled || status === 'saving'}
        maxLength={20}
        autoCapitalize="off"
        autoCorrect="off"
        spellCheck={false}
        inputMode="text"
        enterKeyHint="done"
        placeholder={position.charAt(0).toUpperCase() + position.slice(1) + ' clue'}
        aria-invalid={hasValidationError ? "true" : undefined}
        aria-describedby={hasValidationError && firstError ? errorMessageId : undefined}
        className={`w-full px-3 py-2 rounded-lg text-center shadow-lg transition-colors duration-300 outline-none border-2 ${theme.clueFontClass}`}
        style={{
          backgroundColor: theme.clueBgColor,
          color: theme.clueTextColor,
          borderColor: getBorderColor(),
          fontWeight: theme.clueFontWeight,
          fontSize: theme.clueFontSize,
          cursor: explanationIsAvailable && !isCoarse ? 'help' : undefined,
        }}
      />

      {/* Tactile : bouton info explicite (hover indisponible). Tap → toggle du tooltip. */}
      {explanationIsAvailable && isCoarse && (
        <button
          type="button"
          aria-label="Voir l'explication de l'indice"
          aria-expanded={isTooltipVisible}
          onClick={() => setIsTooltipVisible((v) => !v)}
          className="absolute -top-2 -right-2 w-6 h-6 flex items-center justify-center rounded-full bg-blue-500 text-white text-xs font-bold shadow-md leading-none"
        >
          i
        </button>
      )}

      {explanationIsAvailable && (
        <ClueExplanationTooltip
          explanation={explanation as string}
          visible={isTooltipVisible}
          anchorRef={clueAnchorRef}
        />
      )}

      <AnimatePresence>
        {status === 'saving' && (
          <motion.div
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
            className="absolute -top-6 left-1/2 transform -translate-x-1/2 text-xs font-bold text-blue-500"
          >
            Saving...
          </motion.div>
        )}
        {hasValidationError && firstError && (
          <motion.div
            id={errorMessageId}
            initial={{ opacity: 0, y: -4 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            role="alert"
            className="absolute top-full mt-1 left-1/2 -translate-x-1/2 text-xs text-red-600 bg-white/90 px-2 py-1 rounded shadow-md whitespace-nowrap"
          >
            {getClueErrorMessage(firstError)}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
    </div>
  )
}
