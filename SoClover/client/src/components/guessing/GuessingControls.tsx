import React, { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import confetti from 'canvas-confetti'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { SlotPortal, MOBILE_BOARD_CONTROLS_SLOT_ID, PHASE_CTA_SLOT_ID } from '../shared/SlotPortal'
import { playSound } from '../../core/sounds'
import { CONSTANTS } from '../../core/constants'
import { getGuessingValidateLabel } from '../../core/phaseCtaLabels'

export interface GuessingControlsProps {
  isMyBoard: boolean
  isValidationPending: boolean
  isBoardFull: boolean
  isBoardGuessed: boolean
  canMoveToNext: boolean
  remainingAttempts: number
  onValidate: () => void
  onNextBoard: () => void
  rotation: number
  onRotate: (direction: 'left' | 'right') => void
  hasTriedPlacement: boolean
}

export const GuessingControls = React.memo(({
  isMyBoard,
  isValidationPending,
  isBoardFull,
  isBoardGuessed,
  canMoveToNext,
  remainingAttempts,
  onValidate,
  onNextBoard,
  rotation,
  onRotate,
  hasTriedPlacement,
}: GuessingControlsProps) => {
  const { t } = useTranslation('guessing')

  // Confettis + son correct quand le board est complètement deviné
  const prevIsBoardGuessedRef = useRef(false)
  useEffect(() => {
    if (isBoardGuessed && !prevIsBoardGuessedRef.current) {
      playSound('boardValidationCorrect')
      confetti({
        particleCount: 100,
        spread: 70,
        origin: { y: 0.6 },
        colors: ['#4ade80', '#22c55e', '#16a34a'],
      })
    }
    prevIsBoardGuessedRef.current = isBoardGuessed
  }, [isBoardGuessed])

  // Son incorrect quand remainingAttempts diminue sans que le board soit deviné
  const prevRemainingAttemptsRef = useRef(remainingAttempts)
  useEffect(() => {
    if (
      remainingAttempts < prevRemainingAttemptsRef.current &&
      !isBoardGuessed
    ) {
      playSound('boardValidationIncorrect')
    }
    prevRemainingAttemptsRef.current = remainingAttempts
  }, [remainingAttempts, isBoardGuessed])

  // Logique commune au bouton « Valider / Plateau suivant », partagée par la variante
  // desktop (libellés longs, grand) et la variante mobile compacte (un seul mot).
  const validateAction = canMoveToNext ? onNextBoard : onValidate
  const validateDisabled =
    isValidationPending || (!isBoardFull && !canMoveToNext) || (!canMoveToNext && hasTriedPlacement)
  const validateTitle =
    !isMyBoard && !canMoveToNext && hasTriedPlacement
      ? t('triedPlacementWarning')
      : undefined
  const validateColor = canMoveToNext
    ? 'bg-blue-600 hover:bg-blue-700 shadow-blue/30'
    : isBoardFull
      ? 'bg-clover hover:bg-clover-dark shadow-clover/30'
      : 'bg-gray-400'

  // Mobile (HUD fixe sous le chip) : compact, libellé réduit à un mot.
  const mobileValidateButton = (
    <button
      onClick={validateAction}
      disabled={validateDisabled}
      title={validateTitle}
      className={`px-4 py-2 rounded-full text-white font-bold text-sm shadow-lg transition-all active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed ${validateColor}`}
    >
      {isValidationPending ? (
        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
      ) : getGuessingValidateLabel(canMoveToNext, true, t)}
    </button>
  )

  return (
    <div className="flex flex-col items-center gap-1">
      {/* Réserve de hauteur pour un alignement vertical constant (desktop). Sur tablette
          (pointer:coarse) on libère cette réserve (min-h-0) → un maximum de hauteur rendu au plateau. */}
      <div className="flex flex-col items-center gap-2 min-h-0">
        {!isMyBoard && (
          <>
            {/* Rotation dans le HUD haut (slot #mobile-board-controls-slot, toujours monté).
                enableKeyboard défaut true → raccourcis fléchés sur desktop ; sans effet tactile. */}
            <SlotPortal slotId={MOBILE_BOARD_CONTROLS_SLOT_ID}>
              <div>
                <BoardRotationControls
                  rotation={rotation}
                  onRotate={onRotate}
                  disabled={isValidationPending}
                  showLabel={false}
                />
              </div>
            </SlotPortal>

            <SlotPortal slotId={PHASE_CTA_SLOT_ID}>
              <div className="mobile-fixed-cta">
                {mobileValidateButton}
                {/* Compteur compact « restantes/total » à droite du bouton (caché une fois
                    le plateau résolu, où le bouton devient « Plateau suivant »). */}
                {!canMoveToNext && (
                  <span className="bg-white/80 px-2 py-1 rounded-full text-sm font-bold text-gray-700 shadow-sm whitespace-nowrap">
                    {remainingAttempts}/{CONSTANTS.GUESSING_TOTAL_ATTEMPTS}
                  </span>
                )}
              </div>
            </SlotPortal>
          </>
        )}
      </div>

      {/* Mobile (tactile) : la bulle n'est conservée que lorsqu'elle porte un message
          significatif (Bravo / Dommage). Le reste du temps elle est soit vide, soit purement
          instructionnelle (« Vous ne pouvez pas manipuler votre propre plateau »), et se
          superpose visuellement au board → masquée sur coarse. Desktop : toujours affichée. */}
      <div className={`text-center text-gray-700 bg-white/20 px-3 py-1.5 rounded-lg backdrop-blur-md border border-white/30 max-w-sm ${
        !isBoardGuessed && !(remainingAttempts === 0 && !isMyBoard) ? 'hidden' : ''
      }`}>
        <p className="text-sm italic">
          {isBoardGuessed ? (
            <span className="text-clover-dark font-bold text-lg">{t('boardComplete')}</span>
          ) : remainingAttempts === 0 && !isMyBoard ? (
            <span className="text-red-600 font-bold">{t('noAttemptsLeft')}</span>
          ) : (
            <>
              {isMyBoard && ' ' + t('ownBoard')}
            </>
          )}
        </p>
      </div>
    </div>
  )
});
GuessingControls.displayName = 'GuessingControls';
