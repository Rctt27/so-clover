import React, { useEffect, useRef } from 'react'
import { BodyPortal } from '../shared/BodyPortal'
import confetti from 'canvas-confetti'
import { BoardRotationControls } from '../shared/board/BoardRotationControls'
import { MobileBoardControlsPortal } from '../shared/MobileBoardControlsPortal'
import { playSound } from '../../core/sounds'
import { CONSTANTS } from '../../core/constants'

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

  // Logique commune au bouton « Valider / Plateau suivant », partagée par la variante
  // desktop (libellés longs, grand) et la variante mobile compacte (un seul mot).
  const validateAction = canMoveToNext ? onNextBoard : onValidate
  const validateDisabled =
    isValidationPending || (!isBoardFull && !canMoveToNext) || (!canMoveToNext && hasTriedPlacement)
  const validateTitle =
    !isMyBoard && !canMoveToNext && hasTriedPlacement
      ? '⚠️ Au moins une carte est dans une position déjà testée et fausse. Déplacez-la ou tournez-la avant de valider.'
      : undefined
  const validateColor = canMoveToNext
    ? 'bg-blue-600 hover:bg-blue-700 shadow-blue/30'
    : isBoardFull
      ? 'bg-clover hover:bg-clover-dark shadow-clover/30'
      : 'bg-gray-400'

  // Desktop : rendu deux fois dans le DOM (variantes responsive), une seule visible via CSS.
  const validateButton = (
    <button
      onClick={validateAction}
      disabled={validateDisabled}
      title={validateTitle}
      className={`px-7 py-2 [@media(pointer:coarse)]:py-3 rounded-full text-white font-bold text-base shadow-lg transition-all transform hover:scale-105 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed disabled:scale-100 ${validateColor}`}
    >
      {isValidationPending ? (
        <div className="flex items-center gap-2">
          <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
          Validation...
        </div>
      ) : canMoveToNext ? 'Plateau suivant' : 'Valider le plateau'}
    </button>
  )

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
      ) : canMoveToNext ? 'Suivant' : 'Valider'}
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

            {/* Tablette / tactile (pointer:coarse) : la rotation est déplacée dans le HUD
                haut (à gauche du chip timer), et le bouton « Valider » est ancré en position
                fixe sous le chip — sinon il est clippé par le board height-bound + overflow
                de la colonne centrale. Porté vers <body> pour échapper aux transforms
                d'ancêtres (wrapper de phase animé Framer) qui briseraient un position:fixed. */}
            <MobileBoardControlsPortal>
              <div className="coarse-only">
                <BoardRotationControls
                  rotation={rotation}
                  onRotate={onRotate}
                  disabled={isValidationPending}
                  showLabel={false}
                  enableKeyboard={false}
                />
              </div>
            </MobileBoardControlsPortal>

            <BodyPortal>
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
            </BodyPortal>
          </>
        )}
      </div>

      {/* Mobile (tactile) : la bulle n'est conservée que lorsqu'elle porte un message
          significatif (Bravo / Dommage). Le reste du temps elle est soit vide, soit purement
          instructionnelle (« Vous ne pouvez pas manipuler votre propre plateau »), et se
          superpose visuellement au board → masquée sur coarse. Desktop : toujours affichée. */}
      <div className={`text-center text-gray-700 bg-white/20 px-3 py-1.5 rounded-lg backdrop-blur-md border border-white/30 max-w-sm ${
        !isBoardGuessed && !(remainingAttempts === 0 && !isMyBoard) ? '[@media(pointer:coarse)]:hidden' : ''
      }`}>
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
        {/* Mobile (tactile) : masqué — le compteur « restantes/total » est déjà dans le HUD
            fixe (à droite du bouton « Valider »). Ce doublon restait visible sous le board et
            se superposait lors de la rotation. Desktop : conservé. */}
        {!isBoardGuessed && remainingAttempts > 0 && !isMyBoard && !canMoveToNext && (
          <p className="text-clover-dark font-bold mt-1 text-sm [@media(pointer:coarse)]:hidden">
            Tentatives restantes : {remainingAttempts}
          </p>
        )}
      </div>
    </div>
  )
});
GuessingControls.displayName = 'GuessingControls';
