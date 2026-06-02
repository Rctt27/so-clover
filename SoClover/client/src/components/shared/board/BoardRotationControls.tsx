import { useEffect, type ReactNode } from 'react'

interface BoardRotationControlsProps {
  rotation: number
  onRotate: (direction: 'left' | 'right') => void
  disabled?: boolean
  /** Affiche la pastille « Rotation: x° » entre les deux boutons. Défaut true. */
  showLabel?: boolean
  /** Contenu rendu ENTRE les deux boutons de rotation (remplace la pastille de libellé).
   *  Utilisé pour intercaler le bouton « Valider » entre les flèches (layout tablette). */
  centerSlot?: ReactNode
  /** Enregistre le raccourci clavier flèches ←/→. Défaut true. À désactiver sur les
   *  instances dupliquées (responsive) pour éviter un double-listener / double rotation. */
  enableKeyboard?: boolean
}

export const BoardRotationControls = ({
  rotation,
  onRotate,
  disabled = false,
  showLabel = true,
  centerSlot,
  enableKeyboard = true
}: BoardRotationControlsProps) => {
  const normalizedRotation = ((rotation % 360) + 360) % 360

  useEffect(() => {
    if (!enableKeyboard) return
    const handleKeyDown = (e: KeyboardEvent) => {
      if (disabled) return
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return
      if (e.key === 'ArrowLeft') onRotate('left')
      else if (e.key === 'ArrowRight') onRotate('right')
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onRotate, disabled, enableKeyboard])

  return (
    <div className="flex items-center gap-4">
      <button
        onClick={() => onRotate('left')}
        disabled={disabled}
        className={`p-3 rounded-full bg-white shadow-md transition-colors border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clover ${disabled ? 'opacity-50 cursor-not-allowed' : 'hover:bg-gray-50'}`}
        title="Tourner à gauche (←)"
        aria-label="Tourner le plateau à gauche"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
          <path d="M3 3v5h5" />
        </svg>
      </button>

      {centerSlot !== undefined ? (
        centerSlot
      ) : showLabel ? (
        <div className="text-sm font-medium text-gray-500 bg-gray-100 px-4 py-2 rounded-full">
          Rotation: {normalizedRotation}°
        </div>
      ) : null}

      <button
        onClick={() => onRotate('right')}
        disabled={disabled}
        className={`p-3 rounded-full bg-white shadow-md transition-colors border border-gray-200 focus:outline-none focus:ring-2 focus:ring-clover ${disabled ? 'opacity-50 cursor-not-allowed' : 'hover:bg-gray-50'}`}
        title="Tourner à droite (→)"
        aria-label="Tourner le plateau à droite"
      >
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M21 12a9 9 0 1 1-9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
          <path d="M21 3v5h-5" />
        </svg>
      </button>
    </div>
  )
}
