import { useState, useEffect, useCallback } from 'react'
import { motion } from 'framer-motion'
import { useBoardStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { BoardRotationControls } from '../shared/BoardRotationControls'

export const WritingControls = () => {
  const { myBoard, updateMyBoardRotation } = useBoardStore()
  const { submitBoard } = useGameActions()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleRotate = useCallback((direction: 'left' | 'right') => {
    if (!myBoard) return
    const currentRotation = myBoard.rotation || 0
    const newRotation = direction === 'left' ? currentRotation - 90 : currentRotation + 90
    updateMyBoardRotation(newRotation)
  }, [myBoard?.rotation, updateMyBoardRotation])

  // Keyboard navigation for rotation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if user is typing in an input
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return
      }

      if (e.key === 'ArrowLeft') {
        handleRotate('left')
      } else if (e.key === 'ArrowRight') {
        handleRotate('right')
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [handleRotate])

  if (!myBoard) return null

  const allCluesFilled = Object.values(myBoard.clues).every(clue => clue.text.trim() !== '')

  const handleSubmit = async () => {
    if (!allCluesFilled || isSubmitting || myBoard.isSubmitted) return

    setIsSubmitting(true)
    try {
      await submitBoard()
    } catch (error) {
      setIsSubmitting(false)
    }
  }

  const isActuallySubmitted = myBoard.isSubmitted

  return (
    <div className="flex flex-col items-center gap-6 w-full max-w-md mx-auto mt-8">
      <BoardRotationControls
        rotation={myBoard.rotation || 0}
        onRotate={handleRotate}
        disabled={isActuallySubmitted}
      />

      <motion.button
        whileHover={allCluesFilled && !isSubmitting && !isActuallySubmitted ? { scale: 1.02 } : {}}
        whileTap={allCluesFilled && !isSubmitting && !isActuallySubmitted ? { scale: 0.98 } : {}}
        onClick={handleSubmit}
        disabled={!allCluesFilled || isSubmitting || isActuallySubmitted}
        className={`
          w-full py-4 px-8 rounded-xl font-bold text-white transition-all duration-300
          ${isActuallySubmitted 
            ? 'bg-green-500 cursor-default shadow-md' 
            : allCluesFilled && !isSubmitting 
              ? 'bg-clover hover:bg-clover-dark shadow-lg shadow-clover/20' 
              : 'bg-gray-300 cursor-not-allowed'}
        `}
      >
        {isSubmitting ? (
          <span className="flex items-center justify-center gap-2">
            <svg className="animate-spin h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Soumission...
          </span>
        ) : isActuallySubmitted ? (
          'Plateau Soumis ✓'
        ) : (
          allCluesFilled ? 'Soumettre le plateau' : 'Saisissez les 4 indices'
        )}
      </motion.button>

      {!allCluesFilled && !isActuallySubmitted && (
        <p className="text-xs text-gray-400">
          Vous devez renseigner un indice pour chaque paire de mots avant de pouvoir soumettre.
        </p>
      )}

      {isActuallySubmitted && (
        <p className="text-sm font-medium text-green-600 animate-pulse">
          En attente des autres joueurs...
        </p>
      )}
    </div>
  )
}
