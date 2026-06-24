import { useEffect, useState } from 'react'
import { Volume2, VolumeX } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { toggleMute, isMuted } from '../../core/sounds'

/**
 * Bouton rond flottant de contrôle du son.
 * - État ON (par défaut) : icône Volume2, aspect neutre.
 * - État coupé : icône VolumeX (barrée), rouge.
 * Synchronisé avec localStorage via l'évènement `so-clover-mute-changed`.
 */
export const SoundToggleButton = () => {
  const { t } = useTranslation('common')
  const [muted, setMuted] = useState(isMuted)

  useEffect(() => {
    const onMuteChanged = (e: Event) => {
      const detail = (e as CustomEvent<{ muted: boolean }>).detail
      setMuted(detail?.muted ?? isMuted())
    }
    window.addEventListener('so-clover-mute-changed', onMuteChanged)
    return () => window.removeEventListener('so-clover-mute-changed', onMuteChanged)
  }, [])

  const handleToggleMute = () => {
    toggleMute()
    setMuted(isMuted())
  }

  return (
    <button
      onClick={handleToggleMute}
      className={`flex items-center justify-center p-1.5 rounded-full transition-colors ${
        muted
          ? 'text-red-500 hover:bg-red-50'
          : 'text-gray-700 hover:bg-gray-100'
      }`}
      title={muted ? t('sound.unmute') : t('sound.mute')}
      aria-label={muted ? t('sound.unmute') : t('sound.mute')}
    >
      {muted ? <VolumeX size={18} /> : <Volume2 size={18} />}
    </button>
  )
}
