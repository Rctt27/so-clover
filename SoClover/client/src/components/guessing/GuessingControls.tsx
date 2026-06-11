import React, { useEffect, useRef } from 'react'
import confetti from 'canvas-confetti'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { playSound } from '../../core/sounds'

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

  // Bouton « Valider / Plateau suivant » — extrait en const car réutilisé dans les deux
  // variantes responsive (rendu deux fois dans le DOM, une seule visible via CSS).
  const validateButton = (
    <button
      onClick={canMoveToNext ? onNextBoard : onValidate}
      disabled={isValidationPending || (!isBoardFull && !canMoveToNext) || (!canMoveToNext && hasTriedPlacement)}
      title={!isMyBoard && !canMoveToNext && hasTriedPlacement
        ? '⚠️ Au moins une carte est dans une position déjà testée et fausse. Déplacez-la ou tournez-la avant de valider.'
        : undefined}
      className={`px-7 py-2 [@media(pointer:coarse)]:py-3 rounded-full text-white font-bold text-base shadow-lg transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:scale-100 ${
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
  )

  return (
    <div className="flex flex-col items-center gap-3 [@media(pointer:coarse)]:gap-1">
      {/* Réserve de hauteur pour un alignement vertical constant (desktop). Sur tablette
          (pointer:coarse) on libère cette réserve (min-h-0) → un maximum de hauteur rendu au plateau. */}
      <div className="flex flex-col items-center gap-2 min-h-[100px] [@media(pointer:coarse)]:min-h-0">
        {!isMyBoard && (
          <>
            {/* Desktop / souris (pointer:fine) : rotation avec libellé + raccourcis clavier,
                bouton en dessous. Variante par défaut (s'applique aussi si la media query
                n'est pas supportée). */}
            <div className="flex [@media(pointer:coarse)]:hidden flex-col items-center gap-3">
              <BoardRotationControls
                rotation={rotation}
                onRotate={onRotate}
                disabled={isValidationPending}
              />
              {validateButton}
            </div>

            {/* Tablette / tactile (pointer:coarse) : flèches de rotation de part et d'autre du
                bouton « Valider », sans pastille de libellé → une seule rangée plus basse, le
                plateau récupère la hauteur. Détecté par type de pointeur (pas par largeur : une
                tablette en paysage type Galaxy Tab S9 fait 1280px de large). enableKeyboard=false :
                l'instance desktop (toujours montée) porte déjà le listener clavier. */}
            <div className="hidden [@media(pointer:coarse)]:flex">
              <BoardRotationControls
                rotation={rotation}
                onRotate={onRotate}
                disabled={isValidationPending}
                showLabel={false}
                enableKeyboard={false}
                centerSlot={validateButton}
              />
            </div>
          </>
        )}
      </div>

      <div className="text-center text-gray-700 bg-white/20 px-3 py-1.5 rounded-lg backdrop-blur-md border border-white/30 max-w-sm">
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
        {/* Desktop : phrase complète, toujours montée (bascule d'opacité) pour réserver sa
            hauteur et éviter tout saut de layout. Masquée sur tablette (display:none → 0 hauteur). */}
        {!isMyBoard && !canMoveToNext && (
          <p
            className={`text-orange-600 font-semibold mt-1 text-xs transition-opacity duration-200 [@media(pointer:coarse)]:hidden ${
              hasTriedPlacement ? 'opacity-100' : 'opacity-0'
            }`}
            aria-hidden={!hasTriedPlacement}
          >
            ⚠️ Au moins une carte est dans une position déjà testée et fausse. Déplacez-la ou tournez-la avant de valider.
          </p>
        )}
        {/* Tablette : aucun indicateur d'avertissement au-dessus du bouton (ni réserve de
            hauteur, ni reflow du plateau). Le contour orange + icône sur la carte concernée
            suffit ; le message complet s'affiche en infobulle au survol du bouton « Valider ». */}
        {!isBoardGuessed && remainingAttempts > 0 && !isMyBoard && !canMoveToNext && (
          <p className="text-clover-dark font-bold mt-1 text-sm">
            Tentatives restantes : {remainingAttempts}
          </p>
        )}
      </div>
    </div>
  )
});
GuessingControls.displayName = 'GuessingControls';
