import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { useBoardStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { SlotPortal, MOBILE_BOARD_CONTROLS_SLOT_ID, PHASE_CTA_SLOT_ID } from '../shared/SlotPortal'
import { playSound } from '../../core/sounds'
import { getWritingSubmitLabel } from '../../core/phaseCtaLabels'

export const WritingControls = () => {
  const { t } = useTranslation('writing')
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
  // Validation sémantique async (debounce + appel serveur) : tant qu'un indice est en cours
  // de vérification, `canSubmit` reste faux. On l'expose au libellé du bouton pour afficher
  // un état « Vérification… » explicite au lieu de laisser croire que des indices manquent.
  const anyChecking = (['top', 'right', 'bottom', 'left'] as const).some(p => clueValidity[p].isChecking)
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
  const submitDisabled = !canSubmit || isSubmitting || isActuallySubmitted

  // Libellés du CTA, factorisés (variante compacte mobile vs longue desktop).
  const submitState = {
    isSubmitting,
    isSubmitted: isActuallySubmitted,
    canSubmit,
    anyChecking,
    allCluesFilled,
  }
  const mobileSubmitLabel = getWritingSubmitLabel(submitState, true, t)

  return (
    <div className="writing-controls flex flex-col items-center gap-6 w-full max-w-md mx-auto mt-8">
      {/* HUD : rotation projetée dans le HUD haut, à gauche du chip timer.
          Seule instance — keyboard activé (ignore les keyevents quand un input/textarea est focused). */}
      <SlotPortal slotId={MOBILE_BOARD_CONTROLS_SLOT_ID}>
        <div>
          <BoardRotationControls
            rotation={myBoard.rotation || 0}
            onRotate={handleRotate}
            disabled={isActuallySubmitted}
            showLabel={false}
          />
        </div>
      </SlotPortal>

      {/* CTA « Soumettre » : projeté dans le slot bas du cluster HUD (cf. App.tsx), 16px
          sous la ligne rotation/chip. Le cluster est fixe → positionnement géré par son flex. */}
      <SlotPortal slotId={PHASE_CTA_SLOT_ID}>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={submitDisabled}
          className={`mobile-fixed-cta px-4 py-2 rounded-full font-bold text-white text-sm shadow-md transition-colors duration-300 ${
            isActuallySubmitted
              ? 'bg-green-500'
              : canSubmit && !isSubmitting
                ? 'bg-clover'
                : 'bg-gray-300 cursor-not-allowed'
          }`}
        >
          {mobileSubmitLabel}
        </button>
      </SlotPortal>

      {allCluesFilled && !allCluesValid && !isActuallySubmitted && (
        <p className="text-xs text-red-500">
          {t('semanticInvalid')}
        </p>
      )}

      {isActuallySubmitted && (
        <p className="text-sm font-medium text-green-600 animate-pulse">
          {t('waitingOthers')}
        </p>
      )}
    </div>
  )
}
