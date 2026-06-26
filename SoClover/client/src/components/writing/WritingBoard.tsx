import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Board } from '../shared/board/Board'
import { useBoardStore, useGameStore } from '../../core/store'
import { useGameActions } from '../../hooks/useGameActions'
import { WritingControls } from './WritingControls'
import { SubmissionProgress } from './SubmissionProgress'
import { LandscapePrompt } from '../guessing/LandscapePrompt'

export const WritingBoard = () => {
  const { t } = useTranslation('writing')
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
      <div className="flex flex-col items-center justify-center min-h-svh gap-4">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-clover"></div>
        <p className="text-gray-500">{t('loadingBoard')}</p>
        <p className="text-xs text-gray-400">Phase: {phase} | ID: {playerId}</p>
      </div>
    )
  }

  return (
    <div className="writing-compact-landscape flex flex-col items-center h-[calc(100svh-2rem)] gap-1 w-full max-w-[1200px] mx-auto">
      {/* Incitation au paysage sur mobile portrait : en portrait étroit les indices
          dé-pivotés ne tiennent pas. La saisie se fait en paysage (cf. Axe 6 / Task 3).
          Auto-masqué hors portrait tactile via `.hide-unless-portrait-touch`. */}
      <LandscapePrompt description={t('landscapeHint')} />

      {/* Zone plateau élastique : flex-1 prend la hauteur résiduelle, container-type:size
          en fait le conteneur de référence pour le sizing height-aware du plateau. */}
      <div
        className="flex-1 min-h-0 w-full flex items-center justify-center px-4"
        style={{ containerType: 'size' }}
      >
        <Board
          cards={myBoard.cards}
          rotation={myBoard.rotation}
          animateEntry={true}
          showClueInputs={true}
          containerSized
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

      {/* Pied de page à hauteur auto : toute variation (message soumis, erreur de
          validation) reprend/cède automatiquement de l'espace au plateau. */}
      <div className="shrink-0 w-full flex flex-col items-center">
        <WritingControls />

        {myBoard.isSubmitted && (
          <div className="flex flex-col items-center gap-2 py-4">
            <p className="text-green-600 font-semibold">{t('boardSubmitted')}</p>
            <p className="text-gray-500 text-sm">{t('waitingOthers')}</p>
            <SubmissionProgress />
          </div>
        )}
      </div>
    </div>
  )
}
