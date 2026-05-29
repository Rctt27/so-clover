import { useBoardStore, useGameStore } from '../../core/store'
import type { BoardData } from '../../types/game'

interface SubmissionProgressProps {
  aiOnly?: boolean
}

// Compte les indices déjà posés sur un board (fallback quand aucun event de progression
// n'a encore été reçu) : une direction est « soumise » si son texte d'indice est non vide.
const countSubmittedClues = (board: BoardData | undefined): number => {
  if (!board) return 0
  const { top, right, bottom, left } = board.clues
  return [top, right, bottom, left].filter(c => (c?.text ?? '').trim().length > 0).length
}

export const SubmissionProgress = ({ aiOnly = false }: SubmissionProgressProps) => {
  const myBoard = useBoardStore(s => s.myBoard)
  const otherBoards = useBoardStore(s => s.otherBoards)
  const allPlayers = useGameStore(s => s.players)
  const myPlayerId = useGameStore(s => s.playerId)
  const aiGeneratingPlayerIds = useGameStore(s => s.aiGeneratingPlayerIds)
  const aiClueProgress = useGameStore(s => s.aiClueProgress)

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

      {aiOnly && (
        <ul className="mt-1 flex flex-col gap-1 text-xs text-gray-500">
          {players.map((player) => {
            const board = otherBoards[player.playerId]
            const isSubmitted = board?.isSubmitted ?? false
            const progress = aiClueProgress[player.playerId]
            const submitted = isSubmitted
              ? 4
              : progress?.submitted ?? countSubmittedClues(board)
            const retries = progress?.retries ?? { top: 0, right: 0, bottom: 0, left: 0 }
            return (
              <li key={player.playerId} className="flex items-center gap-2 font-mono">
                <span className="font-medium text-gray-600">{player.name}</span>
                <span>Clues {submitted}/4</span>
                <span className="text-gray-400">·</span>
                <span title="Indices rejetés par direction (Top/Right/Bottom/Left)">
                  ↻ T:{retries.top} R:{retries.right} B:{retries.bottom} L:{retries.left}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
