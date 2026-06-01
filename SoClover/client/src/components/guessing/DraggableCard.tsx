import React, { useState, useRef, useEffect, useCallback } from 'react'
import { motion } from 'framer-motion'
import { CardAssembler } from '../shared/card/CardAssembler'
import { CardInfoResponse, rotationToDegrees } from '../../types/game'
import { useGameStore, useGuessingStore } from '../../core/store'
import { gameApi } from '../../api/game-api'
import correctIcon from '../../assets/images/correctGuess.svg'
import { playSound } from '../../core/sounds'
import { CONSTANTS } from '../../core/constants'
import { LOGICAL_SLOTS } from '../../core/utils'
import { draggableCardArePropsEqual } from './draggableCardArePropsEqual'
import { AlertTriangle } from 'lucide-react'
import { isPlacementAlreadyTried } from '../../core/isPlacementAlreadyTried'

export interface DraggableCardProps {
  card: CardInfoResponse
  index: number
  isOutside: boolean
  isLocked?: boolean
  isCorrect?: boolean
  onClick?: () => void
  isSelected?: boolean
  disabled?: boolean
  isDisplaced?: boolean
  /** Override rotation compensation (used in drag overlay to keep card visually straight) */
  dragRotationOverride?: number
  /** Is this card currently being dragged (source card — hide it) */
  isDragSource?: boolean
  /** Is this card the current drag target (being hovered over) */
  isDragTarget?: boolean
  /** Pointer down handler from createDragHandlers */
  onPointerDown?: (e: React.PointerEvent) => void
  /**
   * Render this card as the floating drag overlay following the cursor.
   * Disables framer-motion `layout`/`layoutId` (which would otherwise animate
   * each position update and cause the card to lag behind the cursor) and the
   * mount fade-in (so the overlay appears instantly).
   */
  isDragOverlay?: boolean
}

const DraggableCardImpl = ({
  card,
  index,
  isOutside,
  isLocked = false,
  isCorrect = false,
  onClick,
  isSelected = false,
  disabled = false,
  isDisplaced = false,
  dragRotationOverride,
  isDragSource = false,
  isDragTarget = false,
  onPointerDown,
  isDragOverlay = false,
}: DraggableCardProps) => {
  const { gameId, playerId, role } = useGameStore()
  const { cumulativeBoardRotation, isValidationPending } = useGuessingStore()
  const validationResults = useGuessingStore((s) => s.validationResults)
  const failedPlacements = useGuessingStore((s) => s.failedPlacements)
  const isLocalDragInProgress = useGuessingStore((s) => s.isLocalDragInProgress)
  const setLocalDragActive = useGuessingStore((s) => s.setLocalDragActive)

  // Position logique de la carte sur le board (undefined pour les cartes du pool)
  const boardPosition: string | undefined = !isOutside ? LOGICAL_SLOTS[index] : undefined
  const isIncorrect =
    boardPosition != null && (validationResults?.incorrectPositions?.includes(boardPosition) ?? false)
  const isNewlyCorrect =
    boardPosition != null && (validationResults?.correctPositions?.includes(boardPosition) ?? false)
  const isAlreadyTried =
    boardPosition != null && !isLocked && !isCorrect &&
    isPlacementAlreadyTried(failedPlacements, card.cardId, boardPosition, card.rotation)

  // Rotation du backend (normalisée 0-270)
  const backendRotation = rotationToDegrees(card.rotation);

  // Rotation continue pour les animations fluides (peut dépasser 360° ou être négative)
  // On garde une trace de la rotation précédente et du cardId pour détecter les changements de carte
  const prevBackendRotationRef = useRef<number | null>(null);
  const prevCardIdRef = useRef<string>(card.cardId);
  const [continuousRotation, setContinuousRotation] = useState(backendRotation);

  // Quand la rotation backend change, calculer le delta et l'appliquer de manière continue
  useEffect(() => {
    // Si la carte a changé, réinitialiser complètement
    if (prevCardIdRef.current !== card.cardId) {
      prevCardIdRef.current = card.cardId;
      prevBackendRotationRef.current = backendRotation;
      setContinuousRotation(backendRotation);
      return;
    }

    if (prevBackendRotationRef.current === null) {
      // Première initialisation
      prevBackendRotationRef.current = backendRotation;
      setContinuousRotation(backendRotation);
      return;
    }

    const prevRotation = prevBackendRotationRef.current;
    let delta = backendRotation - prevRotation;

    // Prendre le chemin le plus court (évite le rollback 270->0 qui ferait -270° au lieu de +90°)
    if (delta > 180) delta -= 360;
    if (delta < -180) delta += 360;

    // Ne mettre à jour que si le delta est non nul
    if (delta !== 0) {
      setContinuousRotation(prev => prev + delta);
      // The backend has now absorbed the local optimistic rotation for this card.
      // Drop the pinned visual offset so the rendered rotation comes solely from continuousRotation.
      if (offsetCardIdRef.current === card.cardId) {
        setRotationVisualOffset(0)
        offsetCardIdRef.current = null
      }
    }
    prevBackendRotationRef.current = backendRotation;
  }, [backendRotation, card.cardId]);

  const canInteract = !isLocked && !disabled && !isValidationPending && role === 'PlayerGuesser'

  const [isRotating, setIsRotating] = useState(false)
  const [rotationVisualOffset, setRotationVisualOffset] = useState(0)
  const cardRef = useRef<HTMLDivElement>(null!)
  const startAngleRef = useRef(0)
  const rotationDirectionRef = useRef<'left' | 'right'>('right')
  const offsetCardIdRef = useRef<string | null>(null)
  const rotationSuppressionTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Nettoyage du timer de suppression si la carte est démontée mid-drag
  useEffect(() => {
    return () => {
      if (rotationSuppressionTimerRef.current !== null) {
        clearTimeout(rotationSuppressionTimerRef.current)
        rotationSuppressionTimerRef.current = null
      }
    }
  }, [])

  const handleRotationStart = useCallback((e: React.PointerEvent, direction: 'left' | 'right') => {
    if (!canInteract || !cardRef.current) return
    e.preventDefault()
    e.stopPropagation()

    rotationDirectionRef.current = direction
    e.currentTarget.setPointerCapture(e.pointerId)

    // Le joueur initie un drag de rotation : supprime les animations locales
    // (cohérent avec un drag de déplacement). Annule un éventuel reset en attente.
    if (rotationSuppressionTimerRef.current !== null) {
      clearTimeout(rotationSuppressionTimerRef.current)
      rotationSuppressionTimerRef.current = null
    }
    setLocalDragActive(true)

    setIsRotating(true)
    const rect = cardRef.current.getBoundingClientRect()
    const centerX = rect.left + rect.width / 2
    const centerY = rect.top + rect.height / 2

    const angle = Math.atan2(e.clientY - centerY, e.clientX - centerX) * (180 / Math.PI)
    startAngleRef.current = angle
  }, [canInteract, setLocalDragActive])

  useEffect(() => {
    if (!isRotating) return

    const handlePointerMove = (e: PointerEvent) => {
      if (!cardRef.current) return

      const rect = cardRef.current.getBoundingClientRect()
      const centerX = rect.left + rect.width / 2
      const centerY = rect.top + rect.height / 2

      const currentAngle = Math.atan2(e.clientY - centerY, e.clientX - centerX) * (180 / Math.PI)
      let delta = currentAngle - startAngleRef.current

      // Ajuster delta pour éviter les sauts brusques (crossing the -180/180 line)
      if (delta > 180) delta -= 360
      if (delta < -180) delta += 360

      // Effet magnet : on snap si on est proche de multiples de 90°
      const snappedDelta = Math.round(delta / 90) * 90
      if (Math.abs(delta - snappedDelta) < 15) {
        setRotationVisualOffset(snappedDelta)
      } else {
        setRotationVisualOffset(delta)
      }
    }

    const handlePointerUp = async () => {
      const steps = Math.round(rotationVisualOffset / 90)

      setIsRotating(false)

      // Maintient le flag de suppression durant le roundtrip serveur, puis libère
      // pour rétablir les animations sur les events SignalR ultérieurs.
      if (rotationSuppressionTimerRef.current !== null) {
        clearTimeout(rotationSuppressionTimerRef.current)
      }
      rotationSuppressionTimerRef.current = setTimeout(() => {
        setLocalDragActive(false)
        rotationSuppressionTimerRef.current = null
      }, 500)

      if (!gameId || !playerId) {
        setRotationVisualOffset(0)
        offsetCardIdRef.current = null
        return
      }

      const finalSteps = steps === 0
        ? (rotationDirectionRef.current === 'right' ? 1 : -1)
        : steps

      // Pin the offset to this card so the next backend update for it triggers absorption.
      offsetCardIdRef.current = card.cardId

      // Son local déclencheur ; spectateurs via `useGameSounds`/`CardRotated` — voir Story 9
      playSound('cardRotate')

      try {
        const SlotPositions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft']
        const boardPosition = !isOutside ? SlotPositions[index] : undefined
        const outsideIndex = isOutside ? index : undefined

        await gameApi.rotateGuessingCard(gameId, playerId, finalSteps, boardPosition, outsideIndex !== undefined ? outsideIndex : undefined)
      } catch (error) {
        console.error('Failed to rotate card:', error)
        // On failure, drop the optimistic offset so the visual returns to truth.
        setRotationVisualOffset(0)
        offsetCardIdRef.current = null
      }
    }

    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', handlePointerUp)
    return () => {
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('pointerup', handlePointerUp)
    }
  }, [isRotating, rotationVisualOffset, gameId, playerId, isOutside, index, setLocalDragActive])

  // La compensation de rotation pour que la carte reste droite face au joueur pendant le drag
  // Si dragRotationOverride est fourni (ex: overlay), on l'utilise directement.
  // Sinon, si la carte vient du board et est en train d'être draggée, on compense la rotation du board.
  // Si elle vient du pool (0°), on ne fait rien.
  const dragRotationCompensation = dragRotationOverride !== undefined
    ? dragRotationOverride
    : (isDragSource ? (isOutside ? 0 : -cumulativeBoardRotation) : 0);

  // Désactiver l'animation de layout pour les actions locales :
  // - `isDisplaced` / `isDragSource` / `isDragOverlay` : suppressions ciblées (existant)
  // - `isLocalDragInProgress` : couvre toute la fenêtre d'un drag local (déplacement
  //   ou rotation) + ~500 ms post-drop. L'initiateur a déjà vu le mouvement via le
  //   drag, donc on supprime l'animation déclenchée par le re-render SignalR.
  //   Les autres joueurs n'ont jamais ce flag set → animation conservée pour eux.
  const shouldAnimateLayout = !isDisplaced && !isDragSource && !isDragOverlay && !isLocalDragInProgress;

  return (
    <motion.div
      ref={cardRef}
      layoutId={shouldAnimateLayout ? `card-${card.cardId}` : undefined}
      layout={shouldAnimateLayout}
      className={`relative transition-shadow duration-200 ${
        isSelected ? 'ring-4 ring-clover rounded-xl shadow-2xl' : 'hover:shadow-lg'
      } ${!canInteract ? 'cursor-default opacity-90' : (isRotating ? 'cursor-grabbing' : 'cursor-grab active:cursor-grabbing')} ${
        isDragTarget ? 'ring-4 ring-amber-400 ring-offset-2 scale-110' : ''
      }`}
      style={{
        width: '100%',
        height: '100%',
        zIndex: isDisplaced ? 150 : (isDragTarget ? 50 : (isDragSource ? 1000 : 'auto')),
        pointerEvents: isDragSource ? 'none' : 'auto',
      }}
      initial={isDragOverlay ? false : { opacity: 0, scale: 0.8 }}
      animate={{
        opacity: 1,
        scale: 1,
        rotate: dragRotationCompensation,
        ...(isCorrect ? {
          scale: [1, 1.1, 1],
        } : {}),
        ...(isDisplaced ? {
          scale: [1.05, 0.98, 1],
        } : {}),
        // Feedback de validation (Story 8.6) — prend le pas sur les autres animations de scale
        ...(isIncorrect ? CONSTANTS.THEME_CONFIG.animations.incorrectShake.animate : {}),
        ...(isNewlyCorrect ? CONSTANTS.THEME_CONFIG.animations.correctPulse.animate : {}),
      }}
      exit={{ opacity: 0, scale: 0.8 }}
      onClick={onClick}
      whileHover={canInteract && !isDragSource && !isRotating ? { scale: 1.05, zIndex: 100 } : {}}
      whileTap={canInteract && !isDragSource && !isRotating ? { scale: 0.95 } : {}}
      transition={{
        layout: { duration: 0.3, ease: 'easeOut' },
        opacity: { duration: 0.2 },
        scale: isDisplaced
          ? { duration: 0.3, ease: 'easeOut' }
          : { duration: 0.2 },
        rotate: { duration: 0 },
      }}
    >
      <motion.div
        className="w-full h-full"
        onPointerDown={onPointerDown}
        style={{
          // Cacher complètement la carte pendant le drag (pas de fade-out)
          // pour donner l'impression que le joueur "possède" la carte
          visibility: isDragSource ? 'hidden' : 'visible'
        }}
      >
        <CardAssembler
          words={[card.topWord, card.rightWord, card.bottomWord, card.leftWord]}
          rotation={continuousRotation + rotationVisualOffset}
          disableAnimation={isDragSource || isDisplaced || isRotating}
        />
      </motion.div>

      {/* Zones de rotation aux coins */}
      {canInteract && !isDragSource && (
        <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 110 }}>
          <div
            className="absolute top-0 left-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onPointerDown={(e) => handleRotationStart(e, 'left')}
            title="Faire pivoter"
          />
          <div
            className="absolute top-0 right-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onPointerDown={(e) => handleRotationStart(e, 'right')}
            title="Faire pivoter"
          />
          <div
            className="absolute bottom-0 left-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onPointerDown={(e) => handleRotationStart(e, 'left')}
            title="Faire pivoter"
          />
          <div
            className="absolute bottom-0 right-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onPointerDown={(e) => handleRotationStart(e, 'right')}
            title="Faire pivoter"
          />
        </div>
      )}

      {/* Icône de succès si correct */}
      {isCorrect && (
        <motion.div
          initial={{ scale: 0, opacity: 0 }}
          animate={{ scale: 1, opacity: 1, rotate: -cumulativeBoardRotation }}
          transition={{ rotate: { duration: 0.5, ease: 'easeInOut' } }}
          className="absolute inset-0 flex items-center justify-center pointer-events-none"
          style={{ zIndex: 120 }}
        >
          <motion.img
            src={correctIcon}
            alt="Correct"
            className="w-16 h-16 drop-shadow-md"
            animate={{
              scale: [1, 1.2, 1],
            }}
            transition={{
              duration: 1,
              repeat: Infinity,
              repeatType: "reverse"
            }}
          />
        </motion.div>
      )}

      {/* Overlay pour les cartes verrouillées */}
      {isLocked && !isCorrect && (
        <div className="absolute inset-0 bg-slate-500/10 rounded-xl pointer-events-none" />
      )}

      {/* Warning : combinaison position/rotation déjà tentée et fausse */}
      {isAlreadyTried && (
        <>
          {/* Outline orange inset — suit le board (pas de contre-rotation) */}
          <div
            className={`absolute inset-0 pointer-events-none ${CONSTANTS.THEME_CONFIG.warningOverlay.outlineClass}`}
            style={{ zIndex: CONSTANTS.THEME_CONFIG.warningOverlay.zIndex }}
          />
          {/* Icône warning top-right — contre-rotée pour rester droite */}
          <div
            className="absolute inset-0 pointer-events-none"
            style={{ zIndex: CONSTANTS.THEME_CONFIG.warningOverlay.zIndex + 1 }}
          >
            <motion.div
              className={`absolute ${CONSTANTS.THEME_CONFIG.warningOverlay.offsetClass}`}
              initial={{ scale: 0, opacity: 0 }}
              animate={{ scale: 1, opacity: 1, rotate: -cumulativeBoardRotation }}
              transition={{ rotate: { duration: 0.5, ease: 'easeInOut' } }}
            >
              <AlertTriangle className={CONSTANTS.THEME_CONFIG.warningOverlay.iconClass} />
            </motion.div>
          </div>
        </>
      )}
    </motion.div>
  )
}

export const DraggableCard = React.memo(DraggableCardImpl, draggableCardArePropsEqual)
DraggableCard.displayName = 'DraggableCard'
