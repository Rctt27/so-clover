import React, { useState, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { CONSTANTS } from '../../core/constants'

export type ClueStatus = 'idle' | 'saving' | 'success' | 'error'

interface ClueInputProps {
  position: 'top' | 'right' | 'bottom' | 'left'
  value: string
  onSave: (value: string) => Promise<void>
  disabled?: boolean
}

export const ClueInput: React.FC<ClueInputProps> = ({ position, value, onSave, disabled }) => {
  const [localValue, setLocalValue] = useState(value)
  const [status, setStatus] = useState<ClueStatus>('idle')
  const inputRef = useRef<HTMLInputElement>(null)

  const REFERENCE_SIZE = 1190
  const INPUT_WIDTH_PX = 220
  const OFFSET_PX = 200 // Distance du bord

  const theme = CONSTANTS.THEME_CONFIG

  useEffect(() => {
    setLocalValue(value)
  }, [value])

  const handleSave = async () => {
    const trimmed = localValue.trim()
    if (trimmed === value || (trimmed === '' && value === '')) {
      setStatus('idle')
      return
    }

    setStatus('saving')
    try {
      await onSave(trimmed)
      setStatus('success')
      setTimeout(() => setStatus('idle'), 2000)
    } catch (error) {
      console.error('[ClueInput] Save failed:', error)
      setStatus('error')
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      inputRef.current?.blur()
    }
  }

  // Calcul du positionnement
  const getPositionStyle = () => {
    let top = '50%'
    let left = '50%'
    let rotation = 0

    switch (position) {
      case 'top':
        top = `${(OFFSET_PX / REFERENCE_SIZE) * 100}%`
        left = '50%'
        rotation = 0
        break
      case 'right':
        top = '50%'
        left = `${((REFERENCE_SIZE - OFFSET_PX) / REFERENCE_SIZE) * 100}%`
        rotation = 90
        break
      case 'bottom':
        top = `${((REFERENCE_SIZE - OFFSET_PX) / REFERENCE_SIZE) * 100}%`
        left = '50%'
        rotation = 180
        break
      case 'left':
        top = '50%'
        left = `${(OFFSET_PX / REFERENCE_SIZE) * 100}%`
        rotation = -90
        break
    }

    return {
      position: 'absolute' as const,
      top,
      left,
      width: `${(INPUT_WIDTH_PX / REFERENCE_SIZE) * 100}%`,
      transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
      zIndex: 100,
    }
  }

  const getBorderColor = () => {
    switch (status) {
      case 'saving': return theme.clueBorderColorSaving
      case 'error': return theme.clueBorderColorError
      case 'success': return theme.clueBorderColor // On garde le vert ou on pourrait flasher un vert plus brillant
      default: return theme.clueBorderColor
    }
  }

  const shakeAnimation = {
    x: [0, -10, 10, -10, 10, 0],
    transition: { duration: 0.4 }
  }

  return (
    <motion.div 
      style={getPositionStyle()}
      animate={status === 'error' ? shakeAnimation : {}}
    >
      <input
        ref={inputRef}
        type="text"
        value={localValue}
        onChange={(e) => setLocalValue(e.target.value)}
        onBlur={handleSave}
        onKeyDown={handleKeyDown}
        disabled={disabled || status === 'saving'}
        maxLength={20}
        placeholder={position.charAt(0).toUpperCase() + position.slice(1) + ' clue'}
        className={`w-full px-3 py-2 rounded-lg text-center shadow-lg transition-colors duration-300 outline-none border-2 ${theme.clueFontClass}`}
        style={{
          backgroundColor: theme.clueBgColor,
          color: theme.clueTextColor,
          borderColor: getBorderColor(),
          fontWeight: theme.clueFontWeight,
          fontSize: theme.clueFontSize,
        }}
      />
      
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
      </AnimatePresence>
    </motion.div>
  )
}
