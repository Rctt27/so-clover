import React, { useState, useRef, useEffect, useCallback } from 'react'
import { motion } from 'framer-motion'
import { useDraggable } from '@dnd-kit/core'
import { Card } from '../shared/Card'
import { CardInfoResponse, rotationToDegrees } from '../../types/game'
import { useGameStore, useGuessingStore } from '../../core/store'
import { gameApi } from '../../api/game-api'
import correctIcon from '../../assets/images/correctGuess.svg'

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
  /** Override rotation compensation (used in DragOverlay to keep card visually straight) */
  dragRotationOverride?: number
}

export const DraggableCard = ({
  card,
  index,
  isOutside,
  isLocked = false,
  isCorrect = false,
  onClick,
  isSelected = false,
  disabled = false,
  isDisplaced = false,
  dragRotationOverride
}: DraggableCardProps) => {
  const { gameId, playerId, role } = useGameStore()
  const { cumulativeBoardRotation, isValidationPending } = useGuessingStore()

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
    }
    prevBackendRotationRef.current = backendRotation;
  }, [backendRotation, card.cardId]);

  const canInteract = !isLocked && !disabled && !isValidationPending && role === 'PlayerGuesser'

  const [isRotating, setIsRotating] = useState(false)
  const [rotationVisualOffset, setRotationVisualOffset] = useState(0)
  const cardRef = useRef<HTMLDivElement>(null!)
  const startAngleRef = useRef(0)
  
  const id = isOutside ? `outside-${card.cardId}` : `board-${card.cardId}`
  const { attributes, listeners, setNodeRef, transform, isDragging, over } = useDraggable({
    id: id,
    data: {
      card,
      index,
      isOutside
    },
    disabled: !canInteract || isRotating
  })

  const handleRotationStart = useCallback((e: React.MouseEvent) => {
    if (!canInteract || !cardRef.current) return
    e.preventDefault()
    e.stopPropagation()
    
    setIsRotating(true)
    const rect = cardRef.current.getBoundingClientRect()
    const centerX = rect.left + rect.width / 2
    const centerY = rect.top + rect.height / 2
    
    const angle = Math.atan2(e.clientY - centerY, e.clientX - centerX) * (180 / Math.PI)
    startAngleRef.current = angle
  }, [canInteract])

  useEffect(() => {
    if (!isRotating) return

    const handleMouseMove = (e: MouseEvent) => {
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

    const handleMouseUp = async () => {
      const steps = Math.round(rotationVisualOffset / 90)
      
      setIsRotating(false)
      setRotationVisualOffset(0)

      if (gameId && playerId) {
        // Si steps est 0, c'est un clic simple sur le coin -> on tourne de 90° (1 step)
        // Sinon on utilise le nombre de steps calculé par le drag
        const finalSteps = steps === 0 ? 1 : steps;
        
        try {
          const SlotPositions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft']
          const boardPosition = !isOutside ? SlotPositions[index] : undefined
          const outsideIndex = isOutside ? index : undefined

          await gameApi.rotateGuessingCard(gameId, playerId, finalSteps, boardPosition, outsideIndex !== undefined ? outsideIndex : undefined)
          // Note: Pas de fetchGameState ici - SignalR gère la synchronisation pour les autres joueurs
          // et le joueur local a déjà fait la mise à jour visuelle via rotationVisualOffset
        } catch (error) {
          console.error('Failed to rotate card:', error)
        }
      }
    }

    window.addEventListener('mousemove', handleMouseMove)
    window.addEventListener('mouseup', handleMouseUp)
    return () => {
      window.removeEventListener('mousemove', handleMouseMove)
      window.removeEventListener('mouseup', handleMouseUp)
    }
  }, [isRotating, rotationVisualOffset, gameId, playerId, isOutside, index])

  // Déterminer si on survole une autre carte pour l'effet de swap
  const isOverOtherCard = over && over.id !== id && (over.id as string).startsWith('board-');

  // On ne veut pas appliquer le transform directement si on utilise DragOverlay
  // Mais on en a besoin pour la position initiale lors du drag start
  const style = transform ? {
    transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`,
    visibility: isDragging ? 'hidden' as const : 'visible' as const,
  } : undefined

  // La compensation de rotation pour que la carte reste droite face au joueur pendant le drag
  // Si dragRotationOverride est fourni (ex: DragOverlay), on l'utilise directement.
  // Sinon, si la carte vient du board et est en train d'être draggée, on compense la rotation du board.
  // Si elle vient du pool (0°), on ne fait rien.
  const dragRotationCompensation = dragRotationOverride !== undefined
    ? dragRotationOverride
    : (isDragging ? (isOutside ? 0 : -cumulativeBoardRotation) : 0);

  // Désactiver l'animation de layout pour les actions locales (isDisplaced = transition en cours)
  // Cela évite l'animation de "vol" de la carte pour le joueur qui fait l'action
  // Les autres joueurs verront toujours l'animation via SignalR car leur isDisplaced sera false
  const shouldAnimateLayout = !isDisplaced && !isDragging;

  return (
    <motion.div
      ref={(node) => {
        setNodeRef(node)
        if (node) cardRef.current = node as HTMLDivElement
      }}
      layoutId={shouldAnimateLayout ? `card-${card.cardId}` : undefined}
      layout={shouldAnimateLayout}
      className={`relative transition-shadow duration-200 ${
        isSelected ? 'ring-4 ring-clover rounded-xl shadow-2xl' : 'hover:shadow-lg'
      } ${!canInteract ? 'cursor-default opacity-90' : (isRotating ? 'cursor-grabbing' : 'cursor-grab active:cursor-grabbing')} ${
        isOverOtherCard ? 'ring-4 ring-amber-400 ring-offset-2 scale-110' : ''
      }`}
      style={{
        width: '100%',
        height: '100%',
        zIndex: isDisplaced ? 150 : (isOverOtherCard ? 50 : (isDragging ? 1000 : 'auto')),
        pointerEvents: isDragging ? 'none' : 'auto',
        ...style,
      }}
      initial={{ opacity: 0, scale: 0.8 }}
      animate={{
        opacity: 1,
        scale: 1,
        rotate: dragRotationCompensation,
        ...(isCorrect ? {
          scale: [1, 1.1, 1],
        } : {}),
        ...(isDisplaced ? {
          scale: [1, 1.1, 1.1, 1],
        } : {})
      }}
      exit={{ opacity: 0, scale: 0.8 }}
      onClick={onClick}
      whileHover={canInteract && !isDragging && !isRotating ? { scale: 1.05, zIndex: 100 } : {}}
      whileTap={canInteract && !isDragging && !isRotating ? { scale: 0.95 } : {}}
      transition={{
        layout: { duration: 0.3, ease: 'easeOut' },
        opacity: { duration: 0.2 },
        scale: { duration: 0.2 },
        rotate: { duration: 0 },
        ...(isDisplaced ? { duration: 0.8, times: [0, 0.2, 0.8, 1] } : {})
      }}
    >
      <motion.div
        className="w-full h-full"
        {...listeners}
        {...attributes}
        style={{
          // Cacher complètement la carte pendant le drag (pas de fade-out)
          // pour donner l'impression que le joueur "possède" la carte
          visibility: isDragging ? 'hidden' : 'visible'
        }}
      >
        <Card
          words={[card.topWord, card.rightWord, card.bottomWord, card.leftWord]}
          rotation={continuousRotation + rotationVisualOffset}
          disableAnimation={isDragging || isDisplaced || isRotating}
        />
      </motion.div>
      
      {/* Zones de rotation aux coins */}
      {canInteract && !isDragging && (
        <div className="absolute inset-0 pointer-events-none" style={{ zIndex: 110 }}>
          <div 
            className="absolute top-0 left-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onMouseDown={handleRotationStart}
            title="Faire pivoter"
          />
          <div 
            className="absolute top-0 right-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onMouseDown={handleRotationStart}
            title="Faire pivoter"
          />
          <div 
            className="absolute bottom-0 left-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onMouseDown={handleRotationStart}
            title="Faire pivoter"
          />
          <div 
            className="absolute bottom-0 right-0 w-12 h-12 cursor-rotation pointer-events-auto"
            onMouseDown={handleRotationStart}
            title="Faire pivoter"
          />
        </div>
      )}

      {/* Icône de succès si correct */}
      {isCorrect && (
        <motion.div 
          initial={{ scale: 0, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
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
    </motion.div>
  )
}
