import React, { useState, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { CONSTANTS } from '../../../core/constants'
import { useClueValidation } from '../../../hooks/useClueValidation'
import { useBoardStore } from '../../../core/store'
import { getClueErrorMessage } from '../../../core/clueValidationMessages'
import { ClueValidationRejection } from '../../../types/game'

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

  const { validateImmediately } = useClueValidation(position, localValue)
  const validity = useBoardStore((s) => s.clueValidity[position])

  const REFERENCE_SIZE = CONSTANTS.ASSET_REFERENCES.board.referenceSize
  const { width: INPUT_WIDTH_PX, offsetFromEdge: OFFSET_PX } = CONSTANTS.ASSET_REFERENCES.clueInput
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
      console.error('[ClueInput] Save failed:', error)
      setStatus('error')
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') inputRef.current?.blur()
  }

  const getPositionStyle = () => {
    let top = '50%'; let left = '50%'; let rotation = 0
    switch (position) {
      case 'top': top = `${(OFFSET_PX / REFERENCE_SIZE) * 100}%`; left = '50%'; rotation = 0; break
      case 'right': top = '50%'; left = `${((REFERENCE_SIZE - OFFSET_PX) / REFERENCE_SIZE) * 100}%`; rotation = 90; break
      case 'bottom': top = `${((REFERENCE_SIZE - OFFSET_PX) / REFERENCE_SIZE) * 100}%`; left = '50%'; rotation = 180; break
      case 'left': top = '50%'; left = `${(OFFSET_PX / REFERENCE_SIZE) * 100}%`; rotation = -90; break
    }
    return {
      position: 'absolute' as const,
      top, left,
      width: `${(INPUT_WIDTH_PX / REFERENCE_SIZE) * 100}%`,
      transform: `translate(-50%, -50%) rotate(${rotation}deg)`,
      zIndex: 100,
    }
  }

  const hasValidationError = !validity.isValid && localValue.trim().length > 0

  const getBorderColor = () => {
    if (status === 'saving') return theme.clueBorderColorSaving
    if (status === 'error' || hasValidationError) return theme.clueBorderColorError
    const isDirty = localValue.trim() !== value.trim()
    if (isDirty && status === 'idle') return '#2196F3'
    return theme.clueBorderColor
  }

  const shakeAnimation = { x: [0, -10, 10, -10, 10, 0], transition: { duration: 0.4 } }

  const firstError = validity.errors[0]
  const errorMessageId = `clue-error-${position}`

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
        aria-invalid={hasValidationError || undefined}
        aria-describedby={hasValidationError && firstError ? errorMessageId : undefined}
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
  )
}
