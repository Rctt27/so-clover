import { RotateCw, Smartphone } from 'lucide-react'
import { useTranslation } from 'react-i18next'

interface LandscapePromptProps {
  /** Justificatif affiché sous le titre. Par phase : la Déduction garde le défaut,
   *  l'Écriture explique que la saisie des indices se fait en paysage. */
  description?: string
}

/**
 * Overlay « tournez votre appareil » affiché sur les appareils tactiles en portrait.
 * Partagé par les phases Déduction (layout 3-colonnes) ET Écriture (indices dé-pivotés
 * lisibles en paysage) : ces deux layouts ne tiennent pas en portrait étroit → on
 * incite au paysage plutôt que d'empiler verticalement (décision PRD
 * Mobile_Compatibility/00_Overview.md, Axe 3 ; généralisation 2026-06-12).
 *
 * La visibilité est pilotée par la seule classe CSS `hide-unless-portrait-touch`
 * (média-query `(orientation: portrait) and (pointer: coarse)` dans index.css) :
 * aucun JS d'orientation, bascule instantanée à la rotation. Le jeu reste monté
 * sous l'overlay (connexion/state préservés), simplement masqué visuellement. Le
 * titre « Tournez votre appareil » reste constant (point d'ancrage des tests e2e).
 */
export const LandscapePrompt = ({ description }: LandscapePromptProps) => {
  const { t } = useTranslation('guessing')
  const desc = description ?? t('landscapeDefault')
  return (
    <div className="hide-unless-portrait-touch fixed inset-0 z-[100] flex-col items-center justify-center gap-6 bg-clover-light px-8 text-center">
      <div className="relative">
        <Smartphone size={72} className="text-clover-dark" />
        <RotateCw
          size={32}
          className="absolute -right-4 -top-2 text-clover animate-pulse"
        />
      </div>
      <h2 className="text-2xl font-bold text-clover-dark">{t('rotateDevice')}</h2>
      <p className="max-w-xs text-gray-600">{desc}</p>
    </div>
  )
}
