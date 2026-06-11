import { RotateCw, Smartphone } from 'lucide-react'

/**
 * Overlay « tournez votre appareil » affiché en phase Guessing sur les appareils
 * tactiles en portrait. Le layout 3-colonnes (pool | plateau | pool) ne tient pas
 * en portrait étroit → on incite au paysage plutôt que d'empiler verticalement
 * (décision PRD Mobile_Compatibility/00_Overview.md, Axe 3).
 *
 * La visibilité est pilotée par la seule classe CSS `hide-unless-portrait-touch`
 * (média-query `(orientation: portrait) and (pointer: coarse)` dans index.css) :
 * aucun JS d'orientation, bascule instantanée à la rotation. Le jeu reste monté
 * sous l'overlay (connexion/state préservés), simplement masqué visuellement.
 */
export const LandscapePrompt = () => {
  return (
    <div className="hide-unless-portrait-touch fixed inset-0 z-[100] flex-col items-center justify-center gap-6 bg-clover-light px-8 text-center">
      <div className="relative">
        <Smartphone size={72} className="text-clover-dark" />
        <RotateCw
          size={32}
          className="absolute -right-4 -top-2 text-clover animate-pulse"
        />
      </div>
      <h2 className="text-2xl font-bold text-clover-dark">Tournez votre appareil</h2>
      <p className="max-w-xs text-gray-600">
        La phase de déduction se joue en mode paysage pour afficher le plateau et
        les cartes côte à côte.
      </p>
    </div>
  )
}
