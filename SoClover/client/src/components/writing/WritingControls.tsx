import { useState, useCallback } from 'react'
import { motion } from 'framer-motion'
import { useBoardStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { playSound } from '../../core/sounds'

export const WritingControls = () => {
  const { myBoard, updateMyBoardRotation, clueValidity } = useBoardStore()
  const { submitBoard } = useGameActions()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleRotate = useCallback((direction: 'left' | 'right') => {
    if (!myBoard) return
    const currentRotation = myBoard.rotation || 0
    const newRotation = direction === 'left' ? currentRotation - 90 : currentRotation + 90
    playSound('boardRotate')
    updateMyBoardRotation(newRotation)
  }, [myBoard?.rotation, updateMyBoardRotation])

  if (!myBoard) return null

  const allCluesFilled = Object.values(myBoard.clues).every(clue => clue.text.trim() !== '')
  const allCluesValid = (['top', 'right', 'bottom', 'left'] as const).every(p => clueValidity[p].isValid)
  const canSubmit = allCluesFilled && allCluesValid

  const handleSubmit = async () => {
    if (!canSubmit || isSubmitting || myBoard.isSubmitted) return

    setIsSubmitting(true)
    try {
      await submitBoard()
    } catch (error) {
      setIsSubmitting(false)
    }
  }

  const isActuallySubmitted = myBoard.isSubmitted

  return (
    <div className="writing-controls flex flex-col items-center gap-6 w-full max-w-md mx-auto mt-8">
      <BoardRotationControls
        rotation={myBoard.rotation || 0}
        onRotate={handleRotate}
        disabled={isActuallySubmitted}
      />

      <motion.button
        whileHover={canSubmit && !isSubmitting && !isActuallySubmitted ? { scale: 1.02 } : {}}
        whileTap={canSubmit && !isSubmitting && !isActuallySubmitted ? { scale: 0.98 } : {}}
        onClick={handleSubmit}
        disabled={!canSubmit || isSubmitting || isActuallySubmitted}
        className={`
          w-full py-4 px-8 rounded-xl font-bold text-white transition-all duration-300
          ${isActuallySubmitted
            ? 'bg-green-500 cursor-default shadow-md'
            : canSubmit && !isSubmitting
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
          canSubmit ? 'Soumettre le plateau' : allCluesFilled ? 'Corrigez les indices' : 'Saisissez les 4 indices'
        )}
      </motion.button>

      {allCluesFilled && !allCluesValid && !isActuallySubmitted && (
        <p className="text-xs text-red-500">
          Certains indices ne respectent pas les règles sémantiques — corrigez-les avant de soumettre.
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
