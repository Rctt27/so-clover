import { useBoardStore, useGameStore } from '../../core/store'

export const SubmissionProgress = () => {
  const myBoard = useBoardStore(s => s.myBoard)
  const otherBoards = useBoardStore(s => s.otherBoards)
  const players = useGameStore(s => s.players)
  const myPlayerId = useGameStore(s => s.playerId)

  const totalCount = players.length
  const otherSubmittedCount = Object.values(otherBoards).filter(b => b.isSubmitted).length
  const mySubmitted = myBoard?.isSubmitted ? 1 : 0
  const submittedCount = mySubmitted + otherSubmittedCount

  if (totalCount === 0) return null

  return (
    <div className="flex flex-col items-center gap-2">
      <p className="text-sm text-gray-600 font-medium">
        {submittedCount}/{totalCount} joueurs ont soumis leur plateau
      </p>
      <div className="flex gap-2">
        {players.map((player) => {
          const isSubmitted = player.playerId === myPlayerId
            ? myBoard?.isSubmitted ?? false
            : otherBoards[player.playerId]?.isSubmitted ?? false
          return (
            <div
              key={player.playerId}
              className={`w-3 h-3 rounded-full transition-colors duration-500 ${
                isSubmitted ? 'bg-green-500' : 'bg-gray-300'
              }`}
            />
          )
        })}
      </div>
    </div>
  )
}
