import React, { useState, useRef, useEffect, useCallback } from 'react'
import { motion } from 'framer-motion'
import { useDraggable } from '@dnd-kit/core'
import { Card } from '../shared/Card'
import { CardInfoResponse, rotationToDegrees } from '../../types/game'
import { useGameStore, useGuessingStore } from '../../core/store'
import { gameApi } from '../../api/game-api'
import { useGameActions } from '../../hooks/useGameActions'
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
  isDisplaced = false
}: DraggableCardProps) => {
  const { gameId, playerId, role } = useGameStore()
  const { cumulativeBoardRotation, isValidationPending } = useGuessingStore()
  const { fetchGameState } = useGameActions()
  
  // Normalisation de la rotation pour éviter les sauts lors des animations
  const boardRotationDeg = !isOutside ? cumulativeBoardRotation : 0;
  const rawRotation = rotationToDegrees(card.rotation) + boardRotationDeg;
  const rotation = (rawRotation % 360 + 360) % 360
  
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

      if (steps !== 0 && gameId && playerId) {
        try {
          const SlotPositions = ['TopLeft', 'TopRight', 'BottomRight', 'BottomLeft']
          const boardPosition = !isOutside ? SlotPositions[index] : undefined
          const outsideIndex = isOutside ? index : undefined
          
          await gameApi.rotateGuessingCard(gameId, playerId, steps, boardPosition, outsideIndex !== undefined ? outsideIndex : undefined)
          await fetchGameState(false)
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
  }, [isRotating, rotationVisualOffset, gameId, playerId, isOutside, index, fetchGameState])

  // Déterminer si on survole une autre carte pour l'effet de swap
  const isOverOtherCard = over && over.id !== id && (over.id as string).startsWith('board-');

  // On ne veut pas appliquer le transform directement si on utilise DragOverlay
  const style = (transform) ? {
    transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`,
  } : undefined

  return (
    <motion.div
      ref={(node) => {
        setNodeRef(node)
        if (node) cardRef.current = node as HTMLDivElement
      }}
      layout
      layoutId={`card-${card.cardId}`}
      className={`relative transition-shadow duration-200 ${
        isSelected ? 'ring-4 ring-clover rounded-xl shadow-2xl' : 'hover:shadow-lg'
      } ${!canInteract ? 'cursor-default opacity-90' : (isRotating ? 'cursor-grabbing' : 'cursor-grab active:cursor-grabbing')} ${
        isOverOtherCard ? 'ring-4 ring-amber-400 ring-offset-2 scale-110' : ''
      }`}
      style={{
        width: '100%',
        height: '100%',
        zIndex: isDisplaced ? 150 : (isOverOtherCard ? 50 : (isDragging ? 1000 : 'auto')),
        opacity: isDragging ? 0 : 1,
        pointerEvents: isDragging ? 'none' : 'auto',
        ...style,
      }}
      onClick={onClick}
      whileHover={canInteract && !isDragging && !isRotating ? { scale: 1.05, zIndex: 100 } : {}}
      whileTap={canInteract && !isDragging && !isRotating ? { scale: 0.95 } : {}}
      animate={{
        ...(isCorrect ? { 
          scale: [1, 1.1, 1],
        } : {}),
        ...(isDisplaced ? {
          scale: [1, 1.1, 1.1, 1],
        } : {})
      }}
      transition={{
        ...(isDisplaced ? { duration: 0.8, times: [0, 0.2, 0.8, 1] } : {})
      }}
    >
      <motion.div
        className="w-full h-full"
        {...listeners}
        {...attributes}
        animate={{ 
          scale: isDragging ? 1.05 : 1,
          opacity: isDragging ? 0.4 : 1
        }}
        transition={{ 
          scale: { duration: 0.1 },
          opacity: { duration: 0.1 }
        }}
      >
        <Card 
          words={[card.topWord, card.rightWord, card.bottomWord, card.leftWord]} 
          rotation={rotation + rotationVisualOffset}
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
