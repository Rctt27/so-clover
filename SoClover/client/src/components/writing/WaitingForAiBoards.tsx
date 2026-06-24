import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useGameActions } from '../../hooks/useGameActions'
import { SubmissionProgress } from './SubmissionProgress'

export const WaitingForAiBoards = () => {
  const { t } = useTranslation('writing')
  const { fetchGameState } = useGameActions()

  useEffect(() => {
    fetchGameState()
  }, [fetchGameState])

  return (
    <div className="flex flex-col items-center justify-center min-h-svh gap-6 py-8 w-full max-w-[800px] mx-auto">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-clover-dark mb-2">{t('waitingAi.title')}</h1>
        <p className="text-gray-600">
          {t('waitingAi.subtitle')}
        </p>
      </div>

      <SubmissionProgress aiOnly />
    </div>
  )
}
