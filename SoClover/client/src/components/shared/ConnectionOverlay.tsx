import { useTranslation } from 'react-i18next'
import { ConnectionStatus } from '../../types/game'

interface ConnectionOverlayProps {
  status: ConnectionStatus
}

export const ConnectionOverlay = ({ status }: ConnectionOverlayProps) => {
  const { t } = useTranslation('common')

  if (status === 'Reconnecting') {
    return (
      <div className="fixed inset-0 bg-black/50 z-[200] flex flex-col items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-white mb-4" />
        <p className="text-white text-lg font-semibold">{t('overlay.lostReconnecting')}</p>
      </div>
    )
  }

  if (status === 'Disconnected') {
    return (
      <div className="fixed inset-0 bg-black/70 z-[200] flex flex-col items-center justify-center gap-4">
        <p className="text-white text-xl font-bold">{t('overlay.lost')}</p>
        <button
          onClick={() => window.location.reload()}
          className="bg-white text-gray-900 font-semibold px-6 py-2 rounded-full hover:bg-gray-100 transition-colors"
        >
          {t('overlay.refresh')}
        </button>
      </div>
    )
  }

  return null
}
