import { useState, useEffect, useCallback } from 'react'
import { motion } from 'framer-motion'
import { useBoardStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'

export const WritingControls = () => {
  const { myBoard, updateMyBoardRotation } = useBoardStore()
  const { submitBoard } = useGameActions()
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (!myBoard) return null

  const handleRotate = useCallback((direction: 'left' | 'right') => {
    const currentRotation = myBoard.rotation || 0
    const newRotation = direction === 'left' ? currentRotation - 90 : currentRotation + 90
    updateMyBoardRotation(newRotation)
  }, [myBoard.rotation, updateMyBoardRotation])

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

  const allCluesFilled = Object.values(myBoard.clues).every(clue => clue.text.trim() !== '')

  const handleSubmit = async () => {
    if (!allCluesFilled || isSubmitting) return

    setIsSubmitting(true)
    try {
      await submitBoard()
    } catch (error) {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex flex-col items-center gap-6 w-full max-w-md mx-auto mt-8">
      <div className="flex items-center gap-4">
        <button
          onClick={() => handleRotate('left')}
          className="p-3 rounded-full bg-white shadow-md hover:bg-gray-50 transition-colors border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clover"
          title="Tourner à gauche (←)"
          aria-label="Tourner le plateau à gauche"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
            <path d="M3 3v5h5" />
          </svg>
        </button>

        <div className="text-sm font-medium text-gray-500 bg-gray-100 px-4 py-2 rounded-full">
          Rotation: {((myBoard.rotation % 360 + 360) % 360)}°
        </div>

        <button
          onClick={() => handleRotate('right')}
          className="p-3 rounded-full bg-white shadow-md hover:bg-gray-50 transition-colors border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clover"
          title="Tourner à droite (→)"
          aria-label="Tourner le plateau à droite"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 12a9 9 0 1 1-9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
            <path d="M21 3v5h-5" />
          </svg>
        </button>
      </div>

      <motion.button
        whileHover={allCluesFilled && !isSubmitting ? { scale: 1.02 } : {}}
        whileTap={allCluesFilled && !isSubmitting ? { scale: 0.98 } : {}}
        onClick={handleSubmit}
        disabled={!allCluesFilled || isSubmitting}
        className={`
          w-full py-4 px-8 rounded-xl font-bold text-white transition-all duration-300
          ${allCluesFilled && !isSubmitting 
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
        ) : (
          allCluesFilled ? 'Soumettre le plateau' : 'Saisissez les 4 indices'
        )}
      </motion.button>

      {!allCluesFilled && (
        <p className="text-xs text-gray-400">
          Vous devez renseigner un indice pour chaque paire de mots avant de pouvoir soumettre.
        </p>
      )}
    </div>
  )
}
