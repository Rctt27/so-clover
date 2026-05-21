import { useBoardStore, useGameStore } from '../../core/store'

interface SubmissionProgressProps {
  aiOnly?: boolean
}

export const SubmissionProgress = ({ aiOnly = false }: SubmissionProgressProps) => {
  const myBoard = useBoardStore(s => s.myBoard)
  const otherBoards = useBoardStore(s => s.otherBoards)
  const allPlayers = useGameStore(s => s.players)
  const myPlayerId = useGameStore(s => s.playerId)
  const aiGeneratingPlayerIds = useGameStore(s => s.aiGeneratingPlayerIds)

  const players = aiOnly ? allPlayers.filter(p => p.isAI) : allPlayers
  const totalCount = players.length
  if (totalCount === 0) return null

  const submittedCount = players.filter(player => {
    if (player.playerId === myPlayerId) return myBoard?.isSubmitted ?? false
    return otherBoards[player.playerId]?.isSubmitted ?? false
  }).length

  return (
    <div className="flex flex-col items-center gap-2">
      <p className="text-sm text-gray-600 font-medium">
        {submittedCount}/{totalCount} {aiOnly ? 'plateaux IA soumis' : 'joueurs ont soumis leur plateau'}
      </p>
      <div className="flex gap-2">
        {players.map((player) => {
          const isSubmitted = player.playerId === myPlayerId
            ? myBoard?.isSubmitted ?? false
            : otherBoards[player.playerId]?.isSubmitted ?? false
          const isGenerating = player.isAI && aiGeneratingPlayerIds.includes(player.playerId)
          return (
            <div
              key={player.playerId}
              title={isGenerating ? 'En cours de génération...' : undefined}
              className={`w-3 h-3 rounded-full transition-colors duration-500 ${
                isSubmitted
                  ? 'bg-green-500'
                  : isGenerating
                    ? 'bg-violet-400 animate-pulse'
                    : 'bg-gray-300'
              }`}
            />
          )
        })}
      </div>
    </div>
  )
}
