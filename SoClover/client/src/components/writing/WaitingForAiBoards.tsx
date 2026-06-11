import { useEffect } from 'react'
import { useGameActions } from '../../hooks/useGameActions'
import { SubmissionProgress } from './SubmissionProgress'

export const WaitingForAiBoards = () => {
  const { fetchGameState } = useGameActions()

  useEffect(() => {
    fetchGameState()
  }, [fetchGameState])

  return (
    <div className="flex flex-col items-center justify-center min-h-svh gap-6 py-8 w-full max-w-[800px] mx-auto">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-clover-dark mb-2">En attente des Boards de l'IA…</h1>
        <p className="text-gray-600">
          Les joueurs IA rédigent leurs indices. La phase de déduction démarrera automatiquement.
        </p>
      </div>

      <SubmissionProgress aiOnly />
    </div>
  )
}
