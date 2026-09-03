import React, { useState, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { CONSTANTS } from '../../../core/constants'
import { computeBoardGeometry, getCluePlacement } from '../../../core/boardGeometry'
import { useClueValidation } from '../../../hooks/useClueValidation'
import { useBoardStore, useAppConfigStore } from '../../../core/store'
import { getClueErrorMessage } from '../../../core/clueValidationMessages'
import { ClueValidationRejection } from '../../../types/game'
import { debugLog } from '../../../core/debug'
import { useCoarsePointer } from '../../../hooks/useCoarsePointer'
import { ClueExplanationTooltip } from './ClueExplanationTooltip'
import { shouldAutoPersistClue } from './shouldAutoPersistClue'

export type ClueStatus = 'idle' | 'saving' | 'success' | 'error'

// Ordre de la cascade d'apparition des pastilles « ? » autour du trèfle : chaque
// direction entre avec un décalage de clueHintStaggerSec × son index.
const CLUE_HINT_CASCADE_ORDER = ['top', 'right', 'bottom', 'left'] as const

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
  const { t } = useTranslation('writing')
  // Visibilité du tooltip d'explication : pilotée par le hover sur desktop, par un
  // tap (bouton info) sur device tactile où le hover n'existe pas (cf. Axe 5 mobile).
  const [isTooltipVisible, setIsTooltipVisible] = useState(false)
  const isCoarse = useCoarsePointer()
  const [localValue, setLocalValue] = useState(value)
  const [status, setStatus] = useState<ClueStatus>('idle')
  const inputRef = useRef<HTMLInputElement>(null)
  const clueAnchorRef = useRef<HTMLDivElement>(null)

  // La vérification sémantique n'est pertinente qu'en édition (phase WritingClues).
  // En lecture seule (Guessing/Scoring), `disabled` est vrai → on ne valide pas.
  const { validateImmediately } = useClueValidation(position, localValue, !disabled)
  const validity = useBoardStore((s) => s.clueValidity[position])

  const clueMaxLength = useAppConfigStore((s) => s.clueMaxLength)
  const boardGeo = computeBoardGeometry(CONSTANTS.ASSET_REFERENCES.board)
  const theme = CONSTANTS.THEME_CONFIG

  useEffect(() => {
    if (document.activeElement !== inputRef.current) {
      setLocalValue(value)
    }
  }, [value])

  // Auto-persistance de l'indice sans attendre le blur : dès que la validation sémantique
  // serveur (debouncée dans useClueValidation) confirme l'indice courant, on l'enregistre.
  // Ainsi le bouton « Soumettre le plateau » s'active dynamiquement une fois les 4 indices
  // enregistrés — le joueur n'a plus à faire un clic « ailleurs » pour quitter le champ.
  //
  // On ne déclenche QUE sur la transition isChecking true→false (l'instant où la validation
  // du `localValue` courant aboutit). C'est la garantie anti-race : `validity` reflète alors
  // le texte courant, jamais une valeur périmée d'un keystroke précédent. On ne passe pas par
  // le statut 'saving' (qui désactiverait l'input et couperait la frappe / fermerait le clavier
  // virtuel sur mobile) : la validation serveur a déjà confirmé l'indice.
  const prevCheckingRef = useRef(validity.isChecking)
  useEffect(() => {
    const settled = prevCheckingRef.current && !validity.isChecking
    prevCheckingRef.current = validity.isChecking
    if (!settled) return
    if (!shouldAutoPersistClue({
      localValue,
      savedValue: value,
      isValid: validity.isValid,
      isChecking: validity.isChecking,
      disabled: !!disabled,
    })) return
    onSave(localValue.trim()).catch((error) => {
      // Échec réseau : laissé au blur (handleSave) qui retentera et affichera l'erreur.
      debugLog('ClueInput', 'Auto-persist failed', error)
    })
  }, [validity.isChecking, validity.isValid, localValue, value, disabled, onSave])

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
    // Indice pivoté le long de sa pétale (rotation corrélée au sens du plateau),
    // identique desktop et mobile (cf. getCluePlacement).
    const { topPct, leftPct, rotation, widthPct } = getCluePlacement(boardGeo, position)
    // Mobile (tactile) : on élargit le champ le long de sa pétale (l'indice est pivoté → la
    // largeur s'étend dans le sens de la pétale, qui est généreuse). Le board mobile est plus
    // petit que le desktop → à largeur identique le champ devient vite à l'étroit pour saisir
    // un indice. Le facteur 1.5 rend de l'espace de frappe sans empiéter sur le cœur du trèfle.
    const widthFactor = isCoarse ? 1.5 : 1
    return {
      position: 'absolute' as const,
      top: `${topPct}%`,
      left: `${leftPct}%`,
      width: `${widthPct * widthFactor}%`,
      transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
      zIndex: 100,
      containerType: 'inline-size' as const,
    }
  }

  const hasValidationError = !validity.isValid && localValue.trim().length > 0

  const getUnderlineColor = () => {
    if (status === 'saving') return theme.clueUnderlineColorSaving
    if (status === 'error' || hasValidationError) return theme.clueUnderlineColorError
    const isDirty = localValue.trim() !== value.trim()
    if (isDirty && status === 'idle') return theme.clueUnderlineColorSaving
    if (status === 'success') return theme.clueUnderlineColorSuccess
    return theme.clueUnderlineColor
  }

  const shakeAnimation = { x: [0, -10, 10, -10, 10, 0], transition: { duration: 0.4 } }

  const firstError = validity.errors[0]
  const errorMessageId = `clue-error-${position}`

  const explanationIsAvailable = !!disabled && !!explanation

  // Le décalage de cascade ne vaut que pour l'apparition : une fois la pastille en place,
  // le fondu d'opacité au survol doit être immédiat. On retombe donc à 0 dès la fin de
  // l'animation d'entrée, et on réarme quand l'explication redisparaît (passage au board
  // suivant → les pastilles rejouent la cascade).
  const [hintHasEntered, setHintHasEntered] = useState(false)
  useEffect(() => {
    if (!explanationIsAvailable) setHintHasEntered(false)
  }, [explanationIsAvailable])
  const hintDelay = hintHasEntered
    ? 0
    : CLUE_HINT_CASCADE_ORDER.indexOf(position) * theme.clueHintStaggerSec

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
    <div data-clue-wrapper data-clue-position={position} style={getPositionStyle()}>
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
        maxLength={clueMaxLength ?? undefined}
        autoCapitalize="off"
        autoCorrect="off"
        spellCheck={false}
        inputMode="text"
        enterKeyHint="done"
        placeholder={t('cluePlaceholder')}
        aria-invalid={hasValidationError ? "true" : undefined}
        aria-describedby={hasValidationError && firstError ? errorMessageId : undefined}
        className={`clue-word w-full px-1 py-2 text-center transition-colors duration-300 outline-none ${theme.clueFontClass}`}
        style={{
          color: theme.clueTextColor,
          // Redesign : pas de remplissage — le champ se fond dans la pétale, seul le
          // souligné d'état le matérialise. Sans ça, le background natif blanc de
          // l'<input> réapparaît (boîte blanche).
          backgroundColor: 'transparent',
          borderBottom: `${theme.clueUnderlineWidth} solid ${getUnderlineColor()}`,
          fontWeight: theme.clueFontWeight,
          fontSize: theme.clueFontSize,
          cursor: explanationIsAvailable && !isCoarse ? 'help' : undefined,
        }}
      />

      {/* Affordance « une explication est consultable » : pastille « ? » collée à droite du
          mot. L'<input> occupe toute la pétale avec un texte centré → impossible de s'ancrer
          sur son bord droit. Ce calque duplique donc l'indice en invisible pour reprendre la
          largeur réelle du texte ; le padding-left compensatoire (pastille + gap) recentre ce
          clone exactement sous le vrai texte, quelle que soit la longueur de l'indice. */}
      {explanationIsAvailable && (
        <div
          className={`clue-word absolute inset-0 flex items-center justify-center pointer-events-none ${theme.clueFontClass}`}
          style={{
            paddingLeft: `calc(${theme.clueHintSizeEm}em + ${theme.clueHintGapEm}em)`,
            fontWeight: theme.clueFontWeight,
            fontSize: theme.clueFontSize,
          }}
        >
          <span style={{ visibility: 'hidden', whiteSpace: 'pre', minWidth: 0, overflow: 'hidden' }}>
            {localValue}
          </span>
          {/* Le halo est un FRERE du bouton, pas un enfant : dans le bouton il hériterait du
              scale d'entrée de Framer et les deux animations se composeraient. Ce wrapper porte
              donc le gap et l'alignement cap-height, le bouton ne garde que sa propre boîte. */}
          <span
            className="relative shrink-0"
            style={{
              marginLeft: `${theme.clueHintGapEm}em`,
              top: `${theme.clueHintCapAlignEm}em`,
              lineHeight: 0,
            }}
          >
            {/* Lueur d'apparition : naît au centre de la pastille, se dilate et se dissipe.
                Même délai de cascade que sa pastille → la lumière et l'éclosion partent
                ensemble. Montée seulement pendant l'entrée puis démontée : aucun re-render
                ultérieur (survol, ouverture du tooltip) ne peut relancer la lueur. */}
            {!hintHasEntered && (
              <motion.span
                aria-hidden="true"
                className="absolute rounded-full pointer-events-none"
                style={{
                  width: `${theme.clueHintSizeEm * theme.clueHintHaloScale}em`,
                  height: `${theme.clueHintSizeEm * theme.clueHintHaloScale}em`,
                  left: '50%',
                  top: '50%',
                  marginLeft: `-${(theme.clueHintSizeEm * theme.clueHintHaloScale) / 2}em`,
                  marginTop: `-${(theme.clueHintSizeEm * theme.clueHintHaloScale) / 2}em`,
                  background: `radial-gradient(circle, rgba(${theme.clueHintHaloColor}, 0.95) 0%, rgba(${theme.clueHintHaloColor}, 0.45) 45%, rgba(${theme.clueHintHaloColor}, 0) 70%)`,
                }}
                initial={{ opacity: 0, scale: 0.35 }}
                animate={{ opacity: [0, theme.clueHintHaloPeakOpacity, 0], scale: [0.35, 1, 1.25] }}
                transition={{
                  duration: theme.clueHintHaloDurationSec,
                  times: [0, 0.35, 1],
                  ease: 'easeOut',
                  delay: hintDelay,
                }}
              />
            )}
            {/* Pointeur fin : le survol de l'indice ouvre déjà le tooltip, la pastille n'est
                qu'un repère visuel → inerte et masquée aux lecteurs d'écran. Tactile : elle est
                le seul déclencheur possible (pas de hover) → vrai bouton. */}
            <motion.button
              type="button"
              aria-hidden={isCoarse ? undefined : true}
              tabIndex={isCoarse ? undefined : -1}
              aria-label={isCoarse ? t('clueExplanationAria') : undefined}
              aria-expanded={isCoarse ? isTooltipVisible : undefined}
              onClick={isCoarse ? () => setIsTooltipVisible((v) => !v) : undefined}
              initial={{ opacity: 0, scale: theme.clueHintEnterScale }}
              animate={{ opacity: isTooltipVisible ? 1 : theme.clueHintIdleOpacity, scale: 1 }}
              transition={{
                scale: { type: 'spring', stiffness: 380, damping: 18, delay: hintDelay },
                opacity: { duration: 0.25, ease: 'easeOut', delay: hintDelay },
              }}
              onAnimationComplete={() => setHintHasEntered(true)}
              className="relative shrink-0 flex items-center justify-center rounded-full leading-none"
              style={{
                width: `${theme.clueHintSizeEm}em`,
                height: `${theme.clueHintSizeEm}em`,
                border: `${theme.clueHintBorderWidth} solid ${theme.clueUnderlineColor}`,
                color: theme.clueTextColor,
                pointerEvents: isCoarse ? 'auto' : 'none',
                cursor: isCoarse ? 'pointer' : undefined,
              }}
            >
              {/* Tactile : la pastille suit la police de l'indice (~13-25px) — trop petit pour
                  un tap. Cette zone invisible débordante élargit la cible sans changer le visuel ;
                  le clic remonte au bouton parent. */}
              {isCoarse && (
                <span aria-hidden="true" className="absolute" style={{ inset: `-${theme.clueHintTapPaddingPx}px` }} />
              )}
              <span
                style={{
                  fontSize: '0.7em',
                  transform: `translate(${theme.clueHintGlyphNudgeXEm}em, ${theme.clueHintGlyphNudgeYEm}em)`,
                }}
              >
                ?
              </span>
            </motion.button>
          </span>
        </div>
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
            {getClueErrorMessage(firstError, t)}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
    </div>
  )
}
