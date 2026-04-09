import { useEffect } from 'react'
import { Board } from '../shared/Board'
import { useBoardStore, useGameStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { WritingControls } from './WritingControls'
import { SubmissionProgress } from './SubmissionProgress'

export const WritingBoard = () => {
  const myBoard = useBoardStore(s => s.myBoard)
  const resetBoards = useBoardStore(s => s.resetBoards)
  const playerId = useGameStore(s => s.playerId)
  const phase = useGameStore(s => s.phase)
  const { fetchGameState, submitClue, loading } = useGameActions()

  // Load board data from backend when entering Writing phase
  useEffect(() => {
    fetchGameState()

    // Setup polling or SignalR would be better here, 
    // but for now we follow the existing logic with improved stability
    
    // Cleanup: reset boards when component unmounts
    return () => {
      resetBoards()
    }
  }, [fetchGameState, resetBoards]) 

  if (!myBoard || loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-screen gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500">Chargement de votre plateau...</p>
        <p className="text-xs text-gray-400">Phase: {phase} | ID: {playerId}</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen gap-8 py-8 w-full max-w-[1200px] mx-auto">
      <div className="text-center">
        <h1 className="text-3xl font-bold text-clover-dark mb-2">Phase d'Écriture</h1>
        <p className="text-gray-600">Observez vos 4 cartes et les 8 mots formés par leurs paires</p>
      </div>

      <div className="flex items-center justify-center w-full px-4 overflow-visible">
        <Board 
          cards={myBoard.cards} 
          rotation={myBoard.rotation} 
          animateEntry={true} 
          showClueInputs={true}
          onClueSave={submitClue}
          disabled={myBoard.isSubmitted}
          clues={{
            top: myBoard.clues.top.text,
            right: myBoard.clues.right.text,
            bottom: myBoard.clues.bottom.text,
            left: myBoard.clues.left.text,
          }}
        />
      </div>

      <WritingControls />

      {myBoard.isSubmitted && (
        <div className="flex flex-col items-center gap-2 py-4">
          <p className="text-green-600 font-semibold">Plateau soumis !</p>
          <p className="text-gray-500 text-sm">En attente des autres joueurs...</p>
          <SubmissionProgress />
        </div>
      )}
    </div>
  )
}
