import React, { useEffect, useRef, useState } from 'react'
import confetti from 'canvas-confetti'
import { Volume2, VolumeX } from 'lucide-react'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { playSound, toggleMute, isMuted } from '../../core/sounds'

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
  // Mute state (synced with localStorage)
  const [muted, setMuted] = useState(isMuted)

  const handleToggleMute = () => {
    toggleMute()
    setMuted(isMuted())
  }

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

  return (
    <div className="flex flex-col items-center gap-6">
      {/* Espace fixe pour maintenir l'alignement vertical constant */}
      <div className="flex flex-col items-center gap-4" style={{ minHeight: '160px' }}>
        {!isMyBoard && (
          <>
            <BoardRotationControls
              rotation={rotation}
              onRotate={onRotate}
              disabled={isValidationPending}
            />

            <button
              onClick={canMoveToNext ? onNextBoard : onValidate}
              disabled={isValidationPending || (!isBoardFull && !canMoveToNext) || (!canMoveToNext && hasTriedPlacement)}
              className={`px-12 py-4 rounded-full text-white font-bold text-xl shadow-lg transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:scale-100 ${
                canMoveToNext ? 'bg-blue-600 hover:bg-blue-700 shadow-blue/30' :
                isBoardFull ? 'bg-clover hover:bg-clover-dark shadow-clover/30' : 'bg-gray-400'
              }`}
            >
              {isValidationPending ? (
                <div className="flex items-center gap-2">
                  <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  Validation...
                </div>
              ) : canMoveToNext ? 'Plateau suivant' : 'Valider le plateau'}
            </button>
          </>
        )}
      </div>

      <div className="text-center text-gray-700 bg-white/20 p-4 rounded-lg backdrop-blur-md border border-white/30 max-w-md">
        <p className="text-sm italic">
          {isBoardGuessed ? (
            <span className="text-clover-dark font-bold text-lg">Bravo ! Plateau complété !</span>
          ) : remainingAttempts === 0 && !isMyBoard ? (
            <span className="text-red-600 font-bold">Dommage ! Plus de tentatives pour ce plateau.</span>
          ) : (
            <>
              {isMyBoard && ' Vous ne pouvez pas manipuler votre propre plateau.'}
            </>
          )}
        </p>
        {/* Toujours monté (quand on devine un plateau) pour réserver sa hauteur : on ne fait
            que basculer l'opacité → aucun reflow/saut de layout quand le warning apparaît. */}
        {!isMyBoard && !canMoveToNext && (
          <p
            className={`text-orange-600 font-semibold mt-2 text-sm transition-opacity duration-200 ${
              hasTriedPlacement ? 'opacity-100' : 'opacity-0'
            }`}
            aria-hidden={!hasTriedPlacement}
          >
            ⚠️ Au moins une carte est dans une position déjà testée et fausse. Déplacez-la ou tournez-la avant de valider.
          </p>
        )}
        {!isBoardGuessed && remainingAttempts > 0 && !isMyBoard && (
          <p className="text-clover-dark font-bold mt-2">
            Tentatives restantes : {remainingAttempts}
          </p>
        )}
      </div>

      {/* Bouton mute */}
      <button
        onClick={handleToggleMute}
        className="flex items-center gap-2 px-3 py-2 rounded-full bg-white/30 hover:bg-white/50 transition-colors text-gray-700 text-sm"
        title={muted ? 'Activer le son' : 'Couper le son'}
      >
        {muted ? <VolumeX size={18} /> : <Volume2 size={18} />}
        <span>{muted ? 'Son coupé' : 'Son activé'}</span>
      </button>
    </div>
  )
});
GuessingControls.displayName = 'GuessingControls';
